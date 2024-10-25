using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AIShell.Abstraction;
using Azure.Core;
using Azure.Identity;
using Serilog;

namespace Microsoft.Azure.Agent;

internal class ChatSession : IDisposable
{
    private const string DL_TOKEN_URL = "https://copilotweb.production.portalrp.azure.com/api/conversations/start?api-version=2024-11-15";
    private const string REFRESH_TOKEN_URL = "https://directline.botframework.com/v3/directline/tokens/refresh";
    private const string CONVERSATION_URL = "https://directline.botframework.com/v3/directline/conversations";

    private string _token;
    private string _streamUrl;
    private string _conversationId;
    private string _conversationUrl;
    private DateTime _expireOn;
    private AzureCopilotReceiver _copilotReceiver;

    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, object> _flights;

    internal string ConversationId => _conversationId;

    internal ChatSession(HttpClient httpClient)
    {
        _httpClient = httpClient;

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
            // ["azurepluginstore"] = true,
        };
    }

    internal async Task<string> RefreshAsync(IStatusContext context, bool force, CancellationToken cancellationToken)
    {
        if (_token is not null)
        {
            if (force)
            {
                // End the existing conversation.
                context.Status("Ending current chat ...");
                EndConversation();
                Reset();
            }
            else
            {
                try
                {
                    context.Status("Refreshing token ...");
                    await RenewTokenAsync(cancellationToken);
                    return null;
                }
                catch (Exception)
                {
                    // Refreshing failed. We will create a new chat session.
                }
            }
        }

        try
        {
            _token = await GenerateTokenAsync(context, cancellationToken);
            return await StartConversationAsync(context, cancellationToken);
        }
        catch (Exception)
        {
            Reset();
            throw;
        }
    }

    private void Reset()
    {
        _token = null;
        _streamUrl = null;
        _conversationId = null;
        _conversationUrl = null;
        _expireOn = DateTime.MinValue;

        _copilotReceiver?.Dispose();
        _copilotReceiver = null;
    }

    private async Task<string> GenerateTokenAsync(IStatusContext context, CancellationToken cancellationToken)
    {
        context.Status("Get Azure CLI login token ...");
        // Get an access token from the AzCLI login, using the specific audience guid.
        AccessToken accessToken = await new AzureCliCredential()
            .GetTokenAsync(
                new TokenRequestContext(["7000789f-b583-4714-ab18-aef39213018a/.default"]),
                cancellationToken);

        context.Status("Request for DirectLine token ...");
        StringContent content = new("{\"conversationType\": \"Chat\"}", Encoding.UTF8, Utils.JsonContentType);
        HttpRequestMessage request = new(HttpMethod.Post, DL_TOKEN_URL) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var dlToken = JsonSerializer.Deserialize<DirectLineToken>(stream, Utils.JsonOptions);
        return dlToken.DirectLine.Token;
    }

    private async Task<string> StartConversationAsync(IStatusContext context, CancellationToken cancellationToken)
    {
        context.Status("Start a new chat session ...");
        HttpRequestMessage request = new(HttpMethod.Post, CONVERSATION_URL);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        SessionPayload spl = JsonSerializer.Deserialize<SessionPayload>(content, Utils.JsonOptions);

        _token = spl.Token;
        _conversationId = spl.ConversationId;
        _conversationUrl = $"{CONVERSATION_URL}/{_conversationId}/activities";
        _streamUrl = spl.StreamUrl;
        _expireOn = DateTime.UtcNow.AddSeconds(spl.ExpiresIn);
        _copilotReceiver = await AzureCopilotReceiver.CreateAsync(_streamUrl);

        Log.Debug("[ChatSession] Conversation started. Id: {0}", _conversationId);

        while (true)
        {
            CopilotActivity activity = _copilotReceiver.Take(cancellationToken);
            if (activity.IsMessage && activity.IsFromCopilot && _copilotReceiver.Watermark is 0)
            {
                activity.ExtractMetadata(out _, out ConversationState conversationState);
                int chatNumber = conversationState.DailyConversationNumber;
                int requestNumber = conversationState.TurnNumber;
                return $"{activity.Text}\nThis is chat #{chatNumber}, request #{requestNumber}.\n";
            }
        }
    }

    private TokenHealth CheckDLTokenHealth()
    {
        ArgumentNullException.ThrowIfNull(_token, nameof(_token));

        var now = DateTime.UtcNow;
        if (now > _expireOn || now.AddMinutes(2) >= _expireOn)
        {
            return TokenHealth.Expired;
        }

        if (now.AddMinutes(10) < _expireOn)
        {
            return TokenHealth.Good;
        }

        return TokenHealth.TimeToRefresh;
    }

    private async Task RenewTokenAsync(CancellationToken cancellationToken)
    {
        TokenHealth health = CheckDLTokenHealth();
        if (health is TokenHealth.Expired)
        {
            Reset();
            throw new TokenRequestException("The chat session has expired. Please start a new chat session.");
        }

        if (health is TokenHealth.Good)
        {
            return;
        }

        try
        {
            HttpRequestMessage request = new(HttpMethod.Post, REFRESH_TOKEN_URL);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
            RefreshDLToken dlToken = JsonSerializer.Deserialize<RefreshDLToken>(content, Utils.JsonOptions);

            _token = dlToken.Token;
            _expireOn = DateTime.UtcNow.AddSeconds(dlToken.ExpiresIn);
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
                    contentType = Utils.JsonContentType,
                    name = "azurecopilot/clienthandlerdefinitions",
                    content =  new {
                        clientHandlers = Array.Empty<object>()
                    }
                },
                new {
                    contentType = Utils.JsonContentType,
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
                    contentType = Utils.JsonContentType,
                    name = "azurecopilot/flights",
                    content = new {
                        flights = _flights
                    }
                }
            },
        };

        var json = JsonSerializer.Serialize(requestData, Utils.JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, Utils.JsonContentType);
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
        using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        JsonObject contentObj = JsonNode.Parse(content).AsObject();
        return contentObj["id"].ToString();
    }

    private void EndConversation()
    {
        if (_token is null || CheckDLTokenHealth() is TokenHealth.Expired)
        {
            // Chat session already expired, no need to send request to end the conversation.
            return;
        }

        var content = new StringContent("{\"type\":\"endOfConversation\",\"from\":{\"id\":\"user\"}}", Encoding.UTF8, Utils.JsonContentType);
        var request = new HttpRequestMessage(HttpMethod.Post, _conversationUrl) { Content = content };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
    }

    internal async Task<CopilotResponse> GetChatResponseAsync(string input, IStatusContext context, CancellationToken cancellationToken)
    {
        try
        {
            context?.Status("Refreshing Token ...");
            await RenewTokenAsync(cancellationToken);

            context?.Status("Sending query ...");
            string activityId = await SendQueryToCopilot(input, cancellationToken);

            while (true)
            {
                CopilotActivity activity = _copilotReceiver.Take(cancellationToken);

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
                        "typing"         => new CopilotResponse(activity, new ChunkReader(_copilotReceiver, activity)),
                        "acceptingInput" => new CopilotResponse(activity),
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
        EndConversation();
        _copilotReceiver?.Dispose();
    }
}
