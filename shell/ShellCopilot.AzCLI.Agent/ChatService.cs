using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using ShellCopilot.Abstraction;

namespace ShellCopilot.AzCLI.Agent;

internal class ChatService : IDisposable
{
    private const string Endpoint = "https://cli-copilot-dogfood.azurewebsites.net/api/CopilotService";

    private readonly HttpClient _client;
    private readonly string[] _scopes;
    private AccessToken? _accessToken;

    internal ChatService()
    {
        _client = new HttpClient();
        _scopes = ["api://62009369-df36-4df2-b7d7-b3e784b3ed55/"];
        _accessToken = null;
    }

    public void Dispose()
    {
        _client.Dispose();
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
            throw new RefreshTokenException(e);
        }
    }

    private HttpRequestMessage PrepareForChat(string input)
    {
        var requestData = new Query { Question = input, Top_num = 1 };
        var json = JsonSerializer.Serialize(requestData, Utils.JsonOptions);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken.Value.Token);
        return request;
    }

    internal async Task<AzCliResponse> GetChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            context?.Status("Refreshing Token ...");
            RefreshToken(cancellationToken);

            context?.Status("Thinking ...");
            HttpRequestMessage request = PrepareForChat(input);
            HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

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
