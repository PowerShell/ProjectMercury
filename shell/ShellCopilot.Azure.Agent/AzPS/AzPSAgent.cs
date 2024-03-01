using Azure.Identity;
using ShellCopilot.Abstraction;
using System.Text.Json;
using System.Diagnostics;
using System.Linq;
using System;

namespace ShellCopilot.Azure.PowerShell;

public sealed class AzPSAgent : ILLMAgent
{
    public string Name => "az-ps";
    public string Description => "An AI assistant to provide Azure PowerShell scripts or commands for managing Azure resources and end-to-end scenarios that involve multiple Azure resources.";
    public Dictionary<string, string> AgentInfo { private set; get; } = null;
    public string SettingFile { private set; get; } = null;

    private const string SettingFileName = "az-ps.agent.json";

    private string _configRoot;
    private RenderingStyle _renderingStyle;
    private AzPSChatService _chatService;
    private MetricHelper _metricHelper;
    private List<HistoryMessage> _historyMessage = [];
    private string _correlationID;

    public void Dispose()
    {
        _chatService.Dispose();
    }

    public void RefreshCorrelationID()
    {
        _correlationID = Guid.NewGuid().ToString();
    }

    public void Initialize(AgentConfig config)
    {
        _renderingStyle = config.RenderingStyle;
        _configRoot = config.ConfigurationRoot;
        SettingFile = Path.Combine(_configRoot, SettingFileName);

        string tenantId = null;
        string subscriptionId = null;
        if (config.Context is not null)
        {
            config.Context.TryGetValue("tenant", out tenantId);
            config.Context.TryGetValue("subscription", out subscriptionId);

            AgentInfo = new Dictionary<string, string>
            {
                ["Tenant"] = tenantId,
                ["Subscription"] = subscriptionId
            };
        }

        _metricHelper = new MetricHelper();
        _chatService = new AzPSChatService(config.IsInteractive, tenantId);
    }

    public IEnumerable<CommandBase> GetCommands() => null;

    public bool CanAcceptFeedback(UserAction action) => true;

    public void OnUserAction(UserActionPayload actionPayload) 
    {
        // DisLike Action
        string DetailedMessage = null;
        if (actionPayload.Action == UserAction.Dislike)
        {
            DislikePayload dislikePayload = (DislikePayload)actionPayload;
            DetailedMessage = string.Format("{0} | {1}", dislikePayload.ShortFeedback, dislikePayload.LongFeedback);
            if (!dislikePayload.ShareConversation)
            {
                ClearHistory();
            }
        }
        // Like Action
        if (actionPayload.Action == UserAction.Like)
        {
            LikePayload likePayload = (LikePayload)actionPayload;
            if (!likePayload.ShareConversation)
            {
                ClearHistory();
            }
        }
        // Other Actions
        if (actionPayload.Action != UserAction.Like && actionPayload.Action != UserAction.Dislike)
        {
            ClearHistory();
        }

        // TODO: Extract into RecrodActionTelemetry : RecordTelemetry()
        _metricHelper.LogTelemetry(AzPSChatService.Endpoint,
                new AzTrace()
                {
                    Command = actionPayload.Action.ToString(),
                    CorrelationID = _correlationID,
                    EventType = "Feedback",
                    Handler = "Azure PowerShell",
                    DetailedMessage = DetailedMessage,
                    HistoryMessage = _historyMessage
                });
    }

    public void RecordQuestionTelemetry()
    {

    }

    public void ClearHistory()
    {
        _historyMessage = [];
    }

    public async Task<bool> Chat(string input, IShell shell)
    {
        // For each Chat input, refresh correlation ID
        RefreshCorrelationID();

        // Measure time spent
        var watch = Stopwatch.StartNew();
        var StartTime = DateTime.Now;

        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        try
        {
            // The AzPS endpoint can return status information in the streaming manner, so we can
            // update the status message while waiting for the answer payload to come back.
            using ChunkReader chunkReader = await host.RunWithSpinnerAsync(
                status: "Thinking ...",
                func: async context => await _chatService.GetStreamingChatResponseAsync(context, input, token, _correlationID, AgentInfo)
            ).ConfigureAwait(false);

            if (chunkReader is null)
            {
                // Operation was cancelled by user.
                return true;
            }

            using var streamingRender = host.NewStreamRender(token);

            try
            {
                while (true)
                {
                    ChunkData chunk = await chunkReader.ReadChunkAsync(token).ConfigureAwait(false);
                    if (chunk is null || chunk.Status.Equals("Finished Generate Answer", StringComparison.Ordinal))
                    {
                        break;
                    }

                    streamingRender.Refresh(chunk.Message);
                }
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled by user.
            }

            _chatService.AddResponseToHistory(streamingRender.AccumulatedContent);

            // Measure time spent
            watch.Stop();

            // TODO: extract into RecordQuestionTelemetry() : RecordTelemetry()
            var EndTime = DateTime.Now;
            var Duration = TimeSpan.FromTicks(watch.ElapsedTicks);

            // Append last Q&A history in HistoryMessage
            for (var index = _chatService._chatHistory.Count-1; index > _chatService._chatHistory.Count - 3; index--)
            {
                _historyMessage.Add(new HistoryMessage
                {
                    CorrelationID = _correlationID,
                    Role = _chatService._chatHistory[index].Role,
                    Content = _chatService._chatHistory[index].Content
                });
            }
            
            _metricHelper.LogTelemetry(AzPSChatService.Endpoint, 
                new AzTrace() {
                    CorrelationID = _correlationID,
                    Duration = Duration,
                    EndTime = EndTime,
                    EventType = "Question",
                    Handler = "Azure PowerShell",
                    StartTime = StartTime
                });
        }
        catch (RefreshTokenException ex)
        {
            Exception inner = ex.InnerException;
            if (inner is CredentialUnavailableException)
            {
                host.MarkupErrorLine($"Access token not available. Query cannot be served.");
                host.MarkupErrorLine($"The '{Name}' agent depends on the Azure PowerShell credential to acquire access token. Please run 'Connect-AzAccount' from a command-line shell to setup account.");
            }
            else
            {
                host.MarkupErrorLine($"Failed to get the access token. {inner.Message}");
            }

            return false;
        }

        return true;
    }
}
