using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

using ShellCopilot.Abstraction;

namespace ShellCopilot.Ollama.Agent;

internal class OllamaChatService : IDisposable
{
    // Ollama endpoint
    internal const string Endpoint = "http://localhost:11434/api/generate";

    private readonly HttpClient _client;
    private readonly string[] _scopes;

    // Access token if endpoint needs authentication
    private AccessToken? _accessToken;

    // optional correlation ID for distinguishing different sessions
    private string _correlationID;

    internal string CorrelationID => _correlationID;

    internal OllamaChatService()
    {
        _client = new HttpClient();
        _scopes = null;
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
        // Main data to send to the endpoint
        var requestData = new Query
        {
            model = "phi3",
            prompt = input,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestData);

        var data = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = data };

        return request;
    }


    internal async Task<ResponseData> GetChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            context?.Status("Generating ...");
            HttpRequestMessage request = PrepareForChat(input);
            HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            context?.Status("Receiving Payload ...");
            Console.Write(response.Content);
            var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            return JsonSerializer.Deserialize<ResponseData>(content);
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled by user.
        }

        return null;
    }
}
