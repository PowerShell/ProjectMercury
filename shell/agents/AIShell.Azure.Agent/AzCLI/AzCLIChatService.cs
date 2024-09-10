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
    internal const string Endpoint = "https://azclitools-copilot-apim-temp.azure-api.net/azcli/copilot";

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
        _chatHistory.Add(new ChatMessage() { Role = "user", Content = input });

        var requestData = new Query { Messages = _chatHistory };
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

            if (response.StatusCode is HttpStatusCode.UnprocessableContent)
            {
                // The AzCLI handler returns status code 422 when the query is out of scope.
                // In this case, we don't save the question to the history.
                _chatHistory.RemoveAt(_chatHistory.Count - 1);
            }
            else
            {
                // Throws if it was not a success response.
                response.EnsureSuccessStatusCode();
            }

            context?.Status("Receiving Payload ...");
            var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            return JsonSerializer.Deserialize<AzCliResponse>(content, Utils.JsonOptions);
        }
        catch (Exception exception)
        {
            // We don't save the question to history when we failed to get a response.
            // Check on history count in case the exception is thrown from token refreshing at the very beginning.
            if (_chatHistory.Count > 0)
            {
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
