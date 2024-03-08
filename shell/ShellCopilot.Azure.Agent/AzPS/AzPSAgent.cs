using Azure.Identity;
using Microsoft.ApplicationInsights;
using ShellCopilot.Abstraction;
using System.Diagnostics;

namespace ShellCopilot.Azure.PowerShell;

public sealed class AzPSAgent : ILLMAgent
{
    public string Name => "az-ps";
    public string Description => "This AI assistant can help provide Azure PowerShell scripts or commands for managing Azure resources and end-to-end scenarios that involve multiple Azure resources.";
    public List<string> SampleQueries => [
        "Create a VM with a public IP address",
        "How to create a web app?",
        "Backup an Azure SQL database to a storage container"
    ];
    public Dictionary<string, string> LegalLinks { private set; get; } = null;
    public string SettingFile { private set; get; } = null;

    private const string SettingFileName = "az-ps.agent.json";

    private string _configRoot;
    private RenderingStyle _renderingStyle;
    private AzPSChatService _chatService;
    private MetricHelper _metricHelper;
    private TelemetryClient _telemetryClient = MetricHelper.InitializeTelemetryClient();
    private List<HistoryMessage> _historyForTelemetry;
    private string _installationID = AzTrace.GetInstallationID();
    private Stopwatch _watch;

    public void Dispose()
    {
        _chatService.Dispose();
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
            config.Context?.TryGetValue("tenant", out tenantId);
            config.Context.TryGetValue("subscription", out subscriptionId);
        }

        LegalLinks = new Dictionary<string, string>
        {
            ["Terms of use"] = "https://aka.ms/TermsOfUse",
            ["Privacy statement"] = "https://aka.ms/privacy",
        };

        _historyForTelemetry = [];
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
                _historyForTelemetry.Clear();
            }
        }
        // Like Action
        else if (actionPayload.Action == UserAction.Like)
        {
            LikePayload likePayload = (LikePayload)actionPayload;
            if (!likePayload.ShareConversation)
            {
                _historyForTelemetry.Clear();
            }
        }

        // TODO: Extract into RecrodActionTelemetry : RecordTelemetry()
        _metricHelper.LogTelemetry(
            _telemetryClient, 
            AzPSChatService.Endpoint,
            new AzTrace()
            {
                Command = actionPayload.Action.ToString(),
                CorrelationID = _chatService.CorrelationID,
                EventType = "Feedback",
                Handler = "Azure PowerShell",
                DetailedMessage = DetailedMessage,
                HistoryMessage = (actionPayload.Action != UserAction.Like && actionPayload.Action != UserAction.Dislike) ? [] : _historyForTelemetry,
                InstallationID = _installationID
            });
    }

    public async Task<bool> Chat(string input, IShell shell)
    {
        // Measure time spent
        _watch = Stopwatch.StartNew();
        var startTime = DateTime.Now;

        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        try
        {
            // The AzPS endpoint can return status information in the streaming manner, so we can
            // update the status message while waiting for the answer payload to come back.
            using ChunkReader chunkReader = await host.RunWithSpinnerAsync(
                status: "Thinking ...",
                func: async context => await _chatService.GetStreamingChatResponseAsync(context, input, token)
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
            _watch.Stop();

            // TODO: extract into RecordQuestionTelemetry() : RecordTelemetry()
            var EndTime = DateTime.Now;
            var Duration = TimeSpan.FromTicks(_watch.ElapsedTicks);

            // Append last Q&A history in HistoryMessage
            _historyForTelemetry.AddRange(new List<HistoryMessage>
            {
                new HistoryMessage
                {
                    CorrelationID = _chatService.CorrelationID,
                    Role = "user",
                    Content = input
                },
                new HistoryMessage
                {
                    CorrelationID = _chatService.CorrelationID,
                    Role = "assistant",
                    Content = streamingRender.AccumulatedContent
                }
            });
            
            _metricHelper.LogTelemetry(
                _telemetryClient,
                AzPSChatService.Endpoint, 
                new AzTrace() {
                    CorrelationID = _chatService.CorrelationID,
                    Duration = Duration,
                    EndTime = EndTime,
                    EventType = "Question",
                    Handler = "Azure PowerShell",
                    InstallationID = _installationID,
                    StartTime = startTime
                });
        }
        catch (RefreshTokenException ex)
        {
            Exception inner = ex.InnerException;
            if (inner is CredentialUnavailableException)
            {
                host.WriteErrorLine($"Access token not available. Query cannot be served.");
                host.WriteErrorLine($"The '{Name}' agent depends on the Azure PowerShell credential to acquire access token. Please run 'Connect-AzAccount' from a command-line shell to setup account.");
            }
            else
            {
                host.WriteErrorLine($"Failed to get the access token. {inner.Message}");
            }

            return false;
        }

        return true;
    }
}
