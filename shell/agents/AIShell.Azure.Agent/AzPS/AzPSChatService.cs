using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using AIShell.Abstraction;

namespace AIShell.Azure.PowerShell;

internal class AzPSChatService : IDisposable
{
    internal const string Endpoint = "https://azclitools-copilot-apim-test.azure-api.net/azps/copilot/streaming";

    private readonly bool _interactive;
    private readonly string[] _scopes;
    private readonly HttpClient _client;
    private readonly List<ChatMessage> _chatHistory;
    private readonly AzurePowerShellCredentialOptions _credOptions;

    private AccessToken? _accessToken;
    private string _correlationID;

    internal string CorrelationID => _correlationID;

    internal AzPSChatService(bool isInteractive, string tenant)
    {
        _interactive = isInteractive;
        _scopes = ["https://management.core.windows.net/"];
        _client = new HttpClient();
        _chatHistory = [];
        _credOptions = string.IsNullOrEmpty(tenant)
            ? null
            : new() { TenantId = tenant };

        _accessToken = null;
        _correlationID = null;
    }

    internal List<ChatMessage> ChatHistory => _chatHistory;

    public void Dispose()
    {
        _client.Dispose();
    }

    internal void AddResponseToHistory(string response)
    {
        if (_interactive && !string.IsNullOrEmpty(response))
        {
            while (_chatHistory.Count > Utils.HistoryCount - 1)
            {
                _chatHistory.RemoveAt(0);
            }
            _chatHistory.Add(new ChatMessage() { Role = "assistant", Content = response });
        }
    }

    private string NewCorrelationID()
    {
        _correlationID = Guid.NewGuid().ToString();
        return _correlationID;
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
            throw new RefreshTokenException("Failed to refresh the Azure PowerShell login token", e);
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

        // These headers are for telemetry. We refresh correlation ID for each query.
        request.Headers.Add("CorrelationId", NewCorrelationID());
        request.Headers.Add("ClientType", "Copilot for client tools");

        return request;
    }

    internal async Task<ChunkReader> GetStreamingChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            context?.Status("Refreshing Token ...");
            RefreshToken(cancellationToken);

            context?.Status("Generating ...");
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
                if (chunk.Status.Equals("Generating the answer", StringComparison.Ordinal))
                {
                    // Received the first chunk for the real answer.
                    // Wrap it along with the reader and return the wrapper.
                    return new ChunkReader(reader, chunk);
                }

                context?.Status(chunk.Status);
            }
        }
        catch (Exception exception)
        {
            // We don't save the question to history when we failed to get a response.
            // Check on history count in case the exception is thrown from token refreshing at the very beginning.
            if (_interactive && _chatHistory.Count > 0)
            {
                // We don't save the question to history when we failed to get a response.
                _chatHistory.RemoveAt(_chatHistory.Count - 1);
            }

            // Re-throw unless the operation was cancelled by user.
            if (exception is not OperationCanceledException)
            {
                throw;
            }
        }

        return null;
    }
}
