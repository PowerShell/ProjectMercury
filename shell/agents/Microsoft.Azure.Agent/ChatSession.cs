using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIShell.Abstraction;

namespace Microsoft.Azure.Agent;

internal class ChatSession : IDisposable
{
    private const string DL_TOKEN_URL = "https://directline.botframework.com/v3/directline/tokens/generate";
    private const string REFRESH_TOKEN_URL = "https://directline.botframework.com/v3/directline/tokens/refresh";
    internal const string CONVERSATION_URL = "https://directline.botframework.com/v3/directline/conversations";

    private string _token;
    private string _streamUrl;
    private string _conversationUrl;
    private DateTime _expireOn;
    private AzureCopilotReceiver _copilotReceiver;

    private readonly string _dl_secret;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, object> _flights;

    internal ChatSession()
    {
        _dl_secret = Environment.GetEnvironmentVariable("DL_SECRET");
        _httpClient = new HttpClient();

        // Keys and values for flights are from the portal request.
        _flights = new Dictionary<string, object>()
        {
            ["openAIModel"] = "gpt4optuc",
            ["openAIEndpointName"] = "norwayeast,australiaeast,westus",
            ["docsHandlerEndpoint"] = "learnDocs",
            ["unifiedcopilotdebug"] = false,
            ["unifiedcopilottest"] = false,
            ["captureintent"] = "true",
            ["capturereasoning"] = "true",
            ["captureparameters"] = "true",
            ["logconversationturn"] = true,
            ["aacopilot"] = true,
            ["corecopilotmanager"] = true,
            ["copilotdirectlinetokennameobjectid"] = true,
            ["disableserverhandlerids"] = "SupportAndTroubleshootBot",
            ["allowcopilotpaneresize"] = true,
            ["copilotmanageability"] = true,
            ["gpt4tcsprompt"] = true,
            ["copilotmanageabilityuimenu"] = true,
            ["usenewchatinputcomponent"] = true, // not sure what this is for
            ["getformstate"] = true,
            ["notificationcopilotbuttonallerror"] = false,
            ["chitchatprompt"] = true,
            // TODO: the streaming is slow and not sending chunks, very clumsy for now.
            // ["streamresponse"] = true,
        };
    }

    internal async Task RefreshAsync(IHost host, CancellationToken cancellationToken)
    {
        try
        {
            await GenerateTokenAsync(host, cancellationToken);
            await StartConversationAsync(host, cancellationToken);
        }
        catch (Exception e)
        {
            Reset();
            if (e is OperationCanceledException)
            {
                host.WriteErrorLine()
                    .WriteErrorLine("Operation cancelled. Please run '/refresh' to start a new conversation.");
            }
            else
            {
                host.WriteErrorLine()
                    .WriteErrorLine($"Failed to start a conversation due to the following error: {e.Message}")
                    .WriteErrorLine(e.StackTrace)
                    .WriteErrorLine()
                    .WriteErrorLine("Please try '/refresh' to start a new conversation.")
                    .WriteErrorLine();
            }
        }
    }

    private void Reset()
    {
        _token = null;
        _streamUrl = null;
        _conversationUrl = null;
        _expireOn = DateTime.MinValue;

        _copilotReceiver?.Dispose();
        _copilotReceiver = null;
    }

    private async Task GenerateTokenAsync(IHost host, CancellationToken cancellationToken)
    {
        // TODO: use spinner when generating token. Also use interaction for authentication is needed.
        string manualToken = Environment.GetEnvironmentVariable("DL_TOKEN");
        if (!string.IsNullOrEmpty(manualToken))
        {
            _token = manualToken;
            return;
        }

        if (string.IsNullOrEmpty(_dl_secret))
        {
            throw new TokenRequestException("You have to manually grab the Direct Line token from portal and set it to the environment variable 'DL_TOKEN' until we figure out authentication.");
        }

        // TODO: figure out how to get the token when copilot API is ready.
        HttpRequestMessage request = new(HttpMethod.Post, DL_TOKEN_URL);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _dl_secret);

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        TokenPayload tpl = JsonSerializer.Deserialize<TokenPayload>(content, Utils.JsonOptions);

        _token = tpl.Token;
    }

    private async Task StartConversationAsync(IHost host, CancellationToken cancellationToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, CONVERSATION_URL);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        SessionPayload spl = JsonSerializer.Deserialize<SessionPayload>(content, Utils.JsonOptions);

        _token = spl.Token;
        _conversationUrl = $"{CONVERSATION_URL}/{spl.ConversationId}/activities";
        _streamUrl = spl.StreamUrl;
        _expireOn = DateTime.UtcNow.AddSeconds(spl.ExpiresIn);
        _copilotReceiver = await AzureCopilotReceiver.CreateAsync(_streamUrl);

        while (true)
        {
            CopilotActivity activity = _copilotReceiver.ActivityQueue.Take(cancellationToken);
            if (activity.IsMessage && activity.IsFromCopilot && _copilotReceiver.Watermark is 0)
            {
                activity.ExtractMetadata(out _, out ConversationState conversationState);
                int chatNumber = conversationState.DailyConversationNumber;
                int requestNumber = conversationState.TurnNumber;

                host.WriteLine($"\n{activity.Text} This is chat #{chatNumber}, request #{requestNumber}.\n");
                return;
            }
        }
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
            type = "message",
            locale = CultureInfo.CurrentCulture.Name,
            from = new { id = "user" },  // TODO, create a uuid to represent the user.
            text = input,
            attachments = new object[] {
                new {
                    contentType = "application/json",
                    name = "azurecopilot/clienthandlerdefinitions",
                    content =  new {
                        clientHandlers = Array.Empty<object>()
                    }
                },
                new {
                    contentType = "application/json",
                    name = "azurecopilot/viewcontext",
                    content = new {
                        viewContext = new {
                            view = new {},
                            additionalDetails = "{\"allContext\":[],\"additionalDetailsString\":\"{}\"}",
                            resourceDetails = new {}
                        }
                    }
                },
                new {
                    contentType = "application/json",
                    name = "azurecopilot/flights",
                    content = new {
                        flights = _flights
                    }
                }
            },
        };

        var json = JsonSerializer.Serialize(requestData, Utils.JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, _conversationUrl) { Content = content };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    private async Task<string> SendQueryToCopilot(string input, CancellationToken cancellationToken)
    {
        // Sending query to Azure Copilot.
        HttpRequestMessage request = PrepareForChat(input);
        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Retrieve the activity id of this query.
        Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        JsonObject contentObj = JsonNode.Parse(content).AsObject();
        return contentObj["id"].ToString();
    }

    internal async Task<CopilotResponse> GetChatResponseAsync(string input, IStatusContext context, CancellationToken cancellationToken)
    {
        try
        {
            // context?.Status("Refreshing Token ...");
            // RenewToken(cancellationToken);

            context?.Status("Generating ...");
            string activityId = await SendQueryToCopilot(input, cancellationToken);

            while (true)
            {
                CopilotActivity activity = _copilotReceiver.ActivityQueue.Take(cancellationToken);

                if (activity.ReplyToId != activityId)
                {
                    // Ignore an activity if it's not a reply to the current activityId.
                    // This may happen when user cancels a response and thus the response activities from copilot were not consumed.
                    continue;
                }

                if (activity.IsTyping)
                {
                    context?.Status(activity.Text);
                    continue;
                }

                if (activity.IsMessage)
                {
                    CopilotResponse ret = activity.InputHint switch
                    {
                        "typing"         => new CopilotResponse(new ChunkReader(_copilotReceiver, activity), activity.TopicName),
                        "acceptingInput" => new CopilotResponse(activity.Text, activity.TopicName),
                        _ => throw CorruptDataException.Create($"The 'inputHint' is {activity.InputHint}.", activity)
                    };

                    if (ret.ChunkReader is null)
                    {
                        activity.ExtractMetadata(out string[] suggestion, out ConversationState state);
                        ret.SuggestedUserResponses = suggestion;
                        ret.ConversationState = state;
                    }

                    return ret;
                }

                throw CorruptDataException.Create($"The 'type' is '{activity.Type}', but we only expect 'typing' and 'message' as we don't support any client handlers.", activity);
            }
        }
        catch (OperationCanceledException)
        {
            // TODO: we may need to notify azure copilot somehow about the cancellation.
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _copilotReceiver?.Dispose();
    }
}
