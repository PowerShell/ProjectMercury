using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Ollama.Agent;

internal class OllamaChatService : IDisposable
{
    internal const string Endpoint = "http://localhost:11434/api/generate";

    private readonly HttpClient _client;
    private readonly string[] _scopes;
    private AccessToken? _accessToken;
    private string _correlationID;

    internal string CorrelationID => _correlationID;

    internal OllamaChatService()
    {
        _client = new HttpClient();
        _scopes = ["https://management.core.windows.net/"];
        _accessToken = null;
        _correlationID = null;
    }


    public void Dispose()
    {
        _client.Dispose();
    }

    private string NewCorrelationID()
    {
        _correlationID = Guid.NewGuid().ToString();
        return _correlationID;
    }

    private HttpRequestMessage PrepareForChat(string input)
    {
        var requestData = new Query
        {
            model = "phi3",
            prompt = input,
            stream = false
        };

        // string jsonPayload = @"{
        //     ""model"": ""phi3"",
        //     ""prompt"": ""How do I create a resource group with azure cli?"",
        //     ""stream"": false
        // }";
        
        var json = JsonSerializer.Serialize(requestData);

        var data = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = data };

        return request;
    }


    internal async Task<OllamaResponse> GetChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            context?.Status("Generating ...");
            HttpRequestMessage request = PrepareForChat(input);
            HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            context?.Status("Receiving Payload ...");
            var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            return JsonSerializer.Deserialize<OllamaResponse>(content);
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled by user.
        }

        return null;
    }
}
