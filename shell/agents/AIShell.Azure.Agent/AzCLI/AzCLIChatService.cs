using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using AIShell.Abstraction;

namespace AIShell.Azure.CLI;

internal class AzCLIChatService : IDisposable
{
    internal const string Endpoint = "https://cli-copilot-dev.azurewebsites.net/api/CopilotService";

    private readonly HttpClient _client;
    private readonly string[] _scopes;
    private readonly List<ChatMessage> _chatHistory;
    private AccessToken? _accessToken;
    private string _correlationID;

    internal string CorrelationID => _correlationID;

    internal AzCLIChatService()
    {
        _client = new HttpClient();
        _scopes = ["https://management.core.windows.net/"];
        _chatHistory = [];
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
        if (!string.IsNullOrEmpty(response))
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
                _accessToken = new AzureCliCredential()
                    .GetToken(new TokenRequestContext(_scopes), cancellationToken);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new RefreshTokenException("Failed to refresh the Azure CLI login token", e);
        }
    }

    private HttpRequestMessage PrepareForChat(string input)
    {
        List<ChatMessage> messages = _chatHistory;
        messages.Add(new ChatMessage() { Role = "user", Content = input });

        var requestData = new Query { Messages = messages };
        var json = JsonSerializer.Serialize(requestData, Utils.JsonOptions);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken.Value.Token);

        // These headers are for telemetry. We refresh correlation ID for each query.
        request.Headers.Add("CorrelationId", NewCorrelationID());
        request.Headers.Add("ClientType", "Copilot for client tools");

        return request;
    }

    internal async Task<AzCliResponse> GetChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            context?.Status("Refreshing Token ...");
            RefreshToken(cancellationToken);

            context?.Status("Generating ...");
            HttpRequestMessage request = PrepareForChat(input);
            HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);

            // The AzCLI handler returns status code 422 when the query is out of scope.
            if (response.StatusCode is not HttpStatusCode.UnprocessableContent)
            {
                response.EnsureSuccessStatusCode();
            }

            context?.Status("Receiving Payload ...");
            var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            return JsonSerializer.Deserialize<AzCliResponse>(content, Utils.JsonOptions);
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled by user.
        }

        return null;
    }
}
