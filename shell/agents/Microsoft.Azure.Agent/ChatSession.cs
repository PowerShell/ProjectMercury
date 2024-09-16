using System.Globalization;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIShell.Abstraction;

namespace Microsoft.Azure.Agent;

internal class ChatSession : IDisposable
{
    // private string? DL_SECRET = Environment.GetEnvironmentVariable("DL_SECRET");
    private const string DL_SECRET = "p01apw-4RCo.0Ck32zNZ6DduKt3Y6ZxOLd-9UBWyg6sr0v4tsTR_n24";
    private const string DL_TOKEN_URL = "https://directline.botframework.com/v3/directline/tokens/generate";
    private const string REFRESH_TOKEN_URL = "https://directline.botframework.com/v3/directline/tokens/refresh";
    internal const string CONVERSATION_URL = "https://directline.botframework.com/v3/directline/conversations";

    private bool _initialized;
    private string _token;
    private string _conversationUrl;
    private string _streamUrl;
    private DateTime _expireOn;
    private ClientWebSocket _webSocket;
    private Task _wsConnectionTask;

    private readonly HttpClient _httpClient;

    internal ChatSession()
    {
        _httpClient = new HttpClient();
    }

    internal void Refresh(CancellationToken cancellationToken)
    {
        try
        {
            GenerateToken(cancellationToken);
            NewChatSession(cancellationToken);

            _initialized = true;
        }
        catch
        {
            Reset();
            throw;
        }
    }

    private void Reset()
    {
        _token = null;
        _conversationUrl = null;
        _streamUrl = null;
        _expireOn = DateTime.MinValue;
        _webSocket = null;
        _wsConnectionTask = null;
        _initialized = false;
    }

    private void GenerateToken(CancellationToken cancellationToken)
    {
        try
        {
            HttpRequestMessage request = new(HttpMethod.Post, DL_TOKEN_URL);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", DL_SECRET);

            HttpResponseMessage response = _httpClient.Send(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            Stream content = response.Content.ReadAsStream(cancellationToken);
            TokenPayload tpl = JsonSerializer.Deserialize<TokenPayload>(content, Utils.JsonOptions);

            _token = tpl.Token;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new TokenRequestException($"Failed to generate the 'DirectLine' token: {e.Message}.", e);
        }
    }

    private void NewChatSession(CancellationToken cancellationToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, CONVERSATION_URL);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        HttpResponseMessage response = _httpClient.Send(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        Stream content = response.Content.ReadAsStream(cancellationToken);
        SessionPayload spl = JsonSerializer.Deserialize<SessionPayload>(content, Utils.JsonOptions);

        _token = spl.Token;
        _conversationUrl = $"{CONVERSATION_URL}/{spl.ConversationId}/activities";
        _streamUrl = spl.StreamUrl;
        _expireOn = DateTime.UtcNow.AddSeconds(spl.ExpiresIn);

        _webSocket = new ClientWebSocket();
        _wsConnectionTask = _webSocket.ConnectAsync(new Uri(spl.StreamUrl), cancellationToken);
    }

    private void RenewToken(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (now > _expireOn || now.AddMinutes(2) >= _expireOn)
        {
            Reset();
            throw new TokenRequestException("The chat session has expired. Please start a new chat session.");
        }
        else if (now.AddMinutes(10) < _expireOn)
        {
            return;
        }

        try
        {
            HttpRequestMessage request = new(HttpMethod.Post, REFRESH_TOKEN_URL);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = _httpClient.Send(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            Stream content = response.Content.ReadAsStream(cancellationToken);
            TokenPayload tpl = JsonSerializer.Deserialize<TokenPayload>(content, Utils.JsonOptions);

            _token = tpl.Token;
            _expireOn = DateTime.UtcNow.AddSeconds(tpl.ExpiresIn);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Reset();
            throw new TokenRequestException($"Failed to refresh the 'DirectLine' token: {e.Message}.", e);
        }
    }

    private HttpRequestMessage PrepareForChat(string input)
    {
        var requestData = new
        {
            locale = CultureInfo.CurrentCulture.Name,
            type = "message",
            from = new { id = "user" },  // TODO, get user information
            text = input
        };

        var json = JsonSerializer.Serialize(requestData, Utils.JsonOptions);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, _conversationUrl) { Content = content };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    private async Task<List<CopilotActivity>> ReceiveActivitiesAsync(MemoryStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        await _wsConnectionTask;
        stream.SetLength(0);

        while (_webSocket.State is WebSocketState.Open)
        {
            var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType is WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Close message received",
                    CancellationToken.None);

                Reset();
                throw new ConnectionDroppedException("Connection to Azure Copilot was dropped unexpectedly.");
            }

            stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage && stream.Length > 0)
            {
                using JsonDocument doc = JsonDocument.Parse(stream);
                JsonElement element = doc.RootElement.GetProperty("activities");
                return element.Deserialize<List<CopilotActivity>>(Utils.JsonOptions);
            }
        }

        throw new ConnectionDroppedException($"Connection to Azure Copilot was dropped unexpectedly (WebSocket state: {_webSocket.State}).");
    }

    internal async Task<CopilotResponse> GetChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            if (_initialized)
            {
                context?.Status("Refreshing Token ...");
                RenewToken(cancellationToken);
            }
            else
            {
                context?.Status("Starting chat session ...");
                Refresh(cancellationToken);
            }

            context?.Status("Generating ...");

            // Sending query to Azure Copilot.
            HttpRequestMessage request = PrepareForChat(input);
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Retrieve the activity id of this query.
            Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
            JsonObject contentObj = JsonNode.Parse(content).AsObject();
            string activityId = contentObj["id"].ToString();

            MemoryStream stream = new();
            byte[] buffer = new byte[512];

            while (true)
            {
                List<CopilotActivity> activities = await ReceiveActivitiesAsync(stream, buffer, cancellationToken);
                foreach (CopilotActivity activity in activities)
                {
                    if (activity.Type is "typing")
                    {
                        context?.Status(activity.Text);
                        continue;
                    }

                    if (activity.Type is "message"
                        && activity.From.Id.StartsWith("copilot", StringComparison.OrdinalIgnoreCase)
                        && activity.ReplyToId == activityId)
                    {
                        CopilotResponse copilotResponse = new() { Text = activity.Text, TopicName = activity.TopicName };
                        foreach (JsonObject jObj in activity.Attachments)
                        {
                            string name = jObj["name"].ToString();
                            if (name is CopilotActivity.SuggestedResponseName)
                            {
                                copilotResponse.SuggestedUserResponses = jObj["content"].Deserialize<string[]>(Utils.JsonOptions);
                            }
                            else if (name is CopilotActivity.ConversationStateName)
                            {
                                copilotResponse.ConversationState = jObj["content"].Deserialize<ConversationState>(Utils.JsonOptions);
                            }
                        }

                        return copilotResponse;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
