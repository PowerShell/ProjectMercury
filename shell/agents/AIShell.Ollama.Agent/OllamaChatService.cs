using System.Text;
using System.Text.Json;

using AIShell.Abstraction;

namespace AIShell.Ollama.Agent;

internal class OllamaChatService : IDisposable
{
    private Settings _settings;
    /// <summary>
    /// Http client 
    /// </summary>
    private readonly HttpClient _client;

    /// <summary>
    /// Initialization method to initialize the http client 
    /// </summary>
    internal OllamaChatService(Settings settings)
    {
        _settings = settings;
        _client = new HttpClient();
    }

    /// <summary>
    /// Refresh settings
    /// </summary>
    /// <param name="settings"></param>
    internal void RefreshSettings(Settings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Dispose of the http client 
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>
    /// Preparing chat with data to be sent
    /// </summary>
    /// <param name="input">The user input from the chat experience</param>
    /// <returns>The HTTP request message</returns>
    private HttpRequestMessage PrepareForChat(string input)
    {
        // Main data to send to the endpoint
        var requestData = new Query
        {
            model = _settings.Model,
            prompt = input,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestData);

        var data = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint) { Content = data };

        return request;
    }

    /// <summary>
    /// Getting the chat response async
    /// </summary>
    /// <param name="context">Interface for the status context used when displaying a spinner.</param>
    /// <param name="input">The user input from the chat experience</param>
    /// <param name="cancellationToken">The cancellation token to exit out of request</param>
    /// <returns>Response data from the API call</returns>
    internal async Task<ResponseData> GetChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            HttpRequestMessage request = PrepareForChat(input);
            HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            context?.Status("Receiving Payload ...");
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
