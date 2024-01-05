using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using ShellCopilot.Abstraction;

namespace ShellCopilot.AzCLI.Agent;

public sealed partial class AzCLIAgent : ILLMAgent
{
    public string Name => "az-cli";
    public string Description { private set; get; }
    public string SettingFile { private set; get; }

    private const string SettingFileName = "az-cli.agent.json";
    private const string Endpoint = "https://cli-copilot-dogfood.azurewebsites.net/api/CopilotService";

    private bool _isInteractive;
    private string _configRoot;
    private RenderingStyle _renderingStyle;
    private HttpClient _client;
    private StringBuilder _text;
    private Regex _listItemPatten;
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

        Description = $"An AI assistant with expertise in Azure CLI topics. I can help with queries about Azure resource management using Azure CLI.";
        SettingFile = Path.Combine(_configRoot, SettingFileName);

        _listItemPatten = ListItemRegex();
        _scopes = ["api://62009369-df36-4df2-b7d7-b3e784b3ed55/"];
        _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
    }

    public IEnumerable<CommandBase> GetCommands()
    {
        return null;
    }

    public async Task<bool> Chat(string input, IShell shell)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        RefreshToken();

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
        _text.AppendLine($"### {data.Scenario}")
            .AppendLine()
            .AppendLine(data.Description)
            .AppendLine();

        if (_listItemPatten.IsMatch(data.Description))
        {
            // When the description itself contains list items, we separate all the
            // actions to another section to make it more readable.
            _text.AppendLine("### Actions to take")
                .AppendLine();
        }

        foreach (Action action in data.CommandSet)
        {
            _text.AppendLine($"- {action.Reason}, using command `{action.Command}`. For example:")
                .AppendLine("```")
                .AppendLine(action.Example)
                .AppendLine("```")
                .AppendLine();
        }

        host.RenderFullResponse(_text.ToString());
        _text.Clear();

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
            // TODO: Need to handle failure. The error message should point the user to login with Az CLI.
            _accessToken = new AzureCliCredential()
                .GetToken(new TokenRequestContext(_scopes));
        }
    }

    [GeneratedRegex("^\\d\\. ", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex ListItemRegex();
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
