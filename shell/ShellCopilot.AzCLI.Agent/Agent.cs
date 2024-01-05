using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using ShellCopilot.Abstraction;

namespace ShellCopilot.AzCLI.Agent;

public sealed class AzCLIAgent : ILLMAgent
{
    public string Name => "az-cli";
    public string Description => @"An AI assistant to get the Azure CLI scripts or commands for management operations of Azure resources and the end-to-end scenarios containing multiple different Azure resources. Sample questions:
  1. Give me a CLI script to create a VM with a public IP address.
  2. How to use Azure CLI script to backup an Azure SQL single database to an Azure storage container?
  3. How to connect an App Service app to an Azure Cache for Redis using Azure CLI?
  4. What is the CLI command to start a SAP system using Azure Center for SAP solutions?";
    public string SettingFile { private set; get; }

    private const string SettingFileName = "az-cli.agent.json";
    private const string Endpoint = "https://cli-copilot-dogfood.azurewebsites.net/api/CopilotService";

    private bool _isInteractive;
    private string _configRoot;
    private RenderingStyle _renderingStyle;
    private HttpClient _client;
    private StringBuilder _text;
    private string[] _scopes;
    private AccessToken? _accessToken;
    private JsonSerializerOptions _jsonOptions;

    public void Dispose()
    {
        _client.Dispose();
    }

    public void Initialize(AgentConfig config)
    {
        _isInteractive = config.IsInteractive;
        _renderingStyle = config.RenderingStyle;
        _configRoot = config.ConfigurationRoot;
        _client = new HttpClient();
        _text = new StringBuilder();

        _scopes = ["api://62009369-df36-4df2-b7d7-b3e784b3ed55/"];
        _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        SettingFile = Path.Combine(_configRoot, SettingFileName);
    }

    public IEnumerable<CommandBase> GetCommands()
    {
        return null;
    }

    public async Task<bool> Chat(string input, IShell shell)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        try
        {
            RefreshToken();
        }
        catch (Exception ex)
        {
            if (ex is CredentialUnavailableException)
            {
                host.MarkupErrorLine($"Access token not available. Query cannot be served.");
                host.MarkupErrorLine($"The '{Name}' agent depends on the Azure CLI credential to aquire access token. Please run 'az login' to setup account.");
            }
            else
            {
                host.MarkupErrorLine($"Failed to get the access token. {ex.Message}");
            }

            return false;
        }

        var requestData = new Query { Question = input, Top_num = 1 };
        var json = JsonSerializer.Serialize(requestData, _jsonOptions);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken.Value.Token);

        Task<HttpResponseMessage> post_func() => _client.SendAsync(requestMessage, token);
        var response = await host.RunWithSpinnerAsync(post_func, "Thinking...").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStreamAsync();
        var azResponse = JsonSerializer.Deserialize<AzCliResponse>(content, _jsonOptions);

        if (azResponse.Error is not null)
        {
            host.MarkupErrorLine(azResponse.Error);
            return true;
        }

        if (azResponse.Data.Count is 0)
        {
            host.MarkupErrorLine("Sorry, no response received.");
            return true;
        }

        var data = azResponse.Data[0];
        _text.Clear();
        _text.AppendLine($"### {data.Scenario}")
            .AppendLine()
            .AppendLine(data.Description)
            .AppendLine();

        if (data.CommandSet.Count > 0)
        {
            _text.AppendLine("```sh");
            for (int i = 0; i < data.CommandSet.Count; i++)
            {
                if (i > 0)
                {
                    _text.AppendLine();
                }

                Action action = data.CommandSet[i];
                _text.AppendLine($"## {action.Reason}, using command `{action.Command}`")
                    .AppendLine(action.Example);
            }
            _text.AppendLine("```")
                .AppendLine()
                .AppendLine("Make sure to replace the argument values used above as appropriate.");
        }

        host.RenderFullResponse(_text.ToString());

        return true;
    }

    private void RefreshToken()
    {
        bool needRefresh = !_accessToken.HasValue;
        if (!needRefresh)
        {
            needRefresh = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(2) > _accessToken.Value.ExpiresOn;
        }

        if (needRefresh)
        {
            _accessToken = new AzureCliCredential()
                .GetToken(new TokenRequestContext(_scopes));
        }
    }
}

internal class Query
{
    public string Question { get; set; }
    public int Top_num { get; set; }
}

internal class Action
{
    public string Command { get; set; }
    public string Reason { get; set; }
    public string Example { get; set; }
    public List<string> Arguments { get; set; }
}

internal class ResponseData
{
    public string Scenario { get; set; }
    public string Description { get; set; }
    public List<Action> CommandSet { get; set; }
}

internal class AzCliResponse
{
    public int Status { get; set; }
    public string Error { get; set; }
    public string Api_version { get; set; }
    public List<ResponseData> Data { get; set; }
}
