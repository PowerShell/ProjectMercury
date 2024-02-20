using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using ShellCopilot.Abstraction;

namespace ShellCopilot.AzPS.Agent;

internal class ChatService : IDisposable
{
    private const string Endpoint = "https://azclitools-copilot.azure-api.net/azps/api/azure-powershell/copilot/streaming";

    private readonly bool _interactive;
    private readonly string[] _scopes;
    private readonly HttpClient _client;
    private readonly List<ChatMessage> _chatHistory;
    private readonly AzurePowerShellCredentialOptions _credOptions;

    private AccessToken? _accessToken;

    internal ChatService(bool isInteractive, string tenant)
    {
        _interactive = isInteractive;
        _scopes = ["https://management.core.windows.net/"];
        _client = new HttpClient();
        _chatHistory = [];
        _credOptions = string.IsNullOrEmpty(tenant)
            ? null
            : new() { TenantId = tenant };

        _accessToken = null;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    internal void AddResponseToHistory(string response)
    {
        if (!string.IsNullOrEmpty(response))
        {
            _chatHistory.Add(new ChatMessage() { Role = "assistant", Content = response });
        }
    }

    private void RefreshToken(CancellationToken cancellationToken)
    {
        try
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
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new RefreshTokenException(e);
        }
    }

    private HttpRequestMessage PrepareForChat(string input, bool streaming)
    {
        List<ChatMessage> messages = _interactive ? _chatHistory : [];
        messages.Add(new ChatMessage() { Role = "user", Content = input });

        var requestData = new Query { Messages = messages, IsStreaming = streaming };
        var json = JsonSerializer.Serialize(requestData, Utils.JsonOptions);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken.Value.Token);
        return request;
    }

    internal async Task<ChunkReader> GetStreamingChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            context?.Status("Refreshing Token ...");
            RefreshToken(cancellationToken);

            context?.Status("Thinking ...");
            HttpRequestMessage request = PrepareForChat(input, streaming: true);
            HttpResponseMessage response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            StreamReader reader = new(stream);

            string line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                var chunk = JsonSerializer.Deserialize<ChunkData>(line, Utils.JsonOptions);
                if (chunk.Status.Equals("Starting Search Examples", StringComparison.Ordinal))
                {
                    context?.Status("Searching Examples ...");
                    continue;
                }

                if (chunk.Status.Equals("Starting Search Cmdlet Reference", StringComparison.Ordinal))
                {
                    context?.Status("Searching Cmdlet Reference ...");
                    continue;
                }

                if (chunk.Status.Equals("Starting Generate Answer", StringComparison.Ordinal))
                {
                    // Received the first chunk for the real answer.
                    // Wrap it along with the reader and return the wrapper.
                    return new ChunkReader(reader, chunk);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled by user.
        }

        return null;
    }
}
