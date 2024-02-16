using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using ShellCopilot.Abstraction;

namespace ShellCopilot.AzPS.Agent;

public sealed class AzPSAgent : ILLMAgent
{
    public string Name => "az-ps";
    public string Description => "An AI assistant to provide Azure PowerShell scripts or commands for managing Azure resources and end-to-end scenarios that involve multiple Azure resources.";
    public Dictionary<string, string> AgentInfo { private set; get; } = null;
    public string SettingFile { private set; get; } = null;

    private const string SettingFileName = "az-ps.agent.json";
    private const string Endpoint = "https://azclitools-copilot.azure-api.net/azps/api/azure-powershell/copilot/streaming";

    private bool _isInteractive;
    private string _configRoot;
    private RenderingStyle _renderingStyle;
    private Dictionary<string, string> _context;
    private HttpClient _client;
    private string[] _scopes;
    private AccessToken? _accessToken;
    private JsonSerializerOptions _jsonOptions;
    private AzurePowerShellCredentialOptions _credOptions;

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

        _context = config.Context;
        if (_context is not null)
        {
            _context.TryGetValue("tenant", out string tenantId);
            _context.TryGetValue("subscription", out string subscriptionId);

            AgentInfo = new Dictionary<string, string>
            {
                ["Tenant"] = tenantId,
                ["Subscription"] = subscriptionId,
            };

            if (tenantId is not null)
            {
                _credOptions = new() { TenantId = tenantId };
            }
        }

        _scopes = ["https://management.core.windows.net/"];
        _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        SettingFile = Path.Combine(_configRoot, SettingFileName);
    }

    public IEnumerable<CommandBase> GetCommands() => null;

    public bool CanAcceptFeedback(UserAction action) => false;
    public void OnUserAction(UserActionPayload actionPayload) {}

    public async Task<bool> Chat(string input, IShell shell)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        try
        {
            RefreshToken(token);
        }
        catch (OperationCanceledException)
        {
            return true;
        }
        catch (Exception ex)
        {
            if (ex is CredentialUnavailableException)
            {
                host.MarkupErrorLine($"Access token not available. Query cannot be served.");
                host.MarkupErrorLine($"The '{Name}' agent depends on the Azure PowerShell credential to acquire access token. Please run 'Connect-AzAccount' from a command-line shell to setup account.");
            }
            else
            {
                host.MarkupErrorLine($"Failed to get the access token. {ex.Message}");
            }

            return false;
        }

        var requestData = new Query { Messages = [ new() { Role = "User", Content = input } ], IsStreaming = true };
        var json = JsonSerializer.Serialize(requestData, _jsonOptions);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken.Value.Token);

        // The AzPS endpoint can return status information in the streaming manner, so we can
        // update the status message while waiting for the answer payload to come back.
        using ReaderWrapper reader = await host.RunWithSpinnerAsync(
            status: "Thinking ...",
            func: async context => {
                try
                {
                    var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();

                    Stream stream = await response.Content.ReadAsStreamAsync(token);
                    StreamReader reader = new(stream);

                    string chunk;
                    while ((chunk = await reader.ReadLineAsync(token)) is not null)
                    {
                        if (chunk.Contains("Starting Search Examples", StringComparison.Ordinal))
                        {
                            context.Status("Searching Examples ...");
                            continue;
                        }

                        if (chunk.Contains("Starting Search Cmdlet Reference", StringComparison.Ordinal))
                        {
                            context.Status("Searching Cmdlet Reference ...");
                            continue;
                        }

                        if (chunk.Contains("Starting Generate Answer", StringComparison.Ordinal))
                        {
                            // Received the first chunk for the real answer.
                            // Wrap it along with the reader and return the wrapper.
                            return new ReaderWrapper(reader, chunk);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ignore the cancellation exception.
                }

                return null;
            }
        ).ConfigureAwait(false);

        if (reader is not null)
        {
            string line;
            using var streamingRender = host.NewStreamRender(token);

            try
            {
                while ((line = await reader.ReadLineAsync(token)) is not null)
                {
                    var chunkData = JsonSerializer.Deserialize<ChunkData>(line, _jsonOptions);
                    if (chunkData.Status.Equals("Finished Generate Answer", StringComparison.Ordinal))
                    {
                        break;
                    }

                    streamingRender.Refresh(chunkData.Message);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore the cancellation exception.
            }

            // Get the accumulated content from `streamingRender` and add it the history collection.
        }

        return true;
    }

    private void RefreshToken(CancellationToken cancellationToken)
    {
        bool needRefresh = !_accessToken.HasValue;
        if (!needRefresh)
        {
            needRefresh = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(2) > _accessToken.Value.ExpiresOn;
        }

        if (needRefresh)
        {
            _accessToken = new AzurePowerShellCredential(_credOptions)
                .GetToken(new TokenRequestContext(_scopes), cancellationToken);
        }
    }
}

internal class ReaderWrapper : IDisposable
{
    private readonly StreamReader _reader;
    private string _current;

    internal ReaderWrapper(StreamReader reader, string currentLine)
    {
        _reader = reader;
        _current = currentLine;
    }

    internal async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (_current is not null)
        {
            string ret = _current;
            _current = null;
            return ret;
        }

        return await _reader.ReadLineAsync(cancellationToken);
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}

internal class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
}

internal class Query
{
    public List<ChatMessage> Messages { get; set; }

    [JsonPropertyName("is_streaming")]
    public bool IsStreaming { get; set; }
}

internal class ChunkData
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }
    public double Created { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
}
