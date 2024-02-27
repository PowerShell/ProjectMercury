using Azure.Identity;
using ShellCopilot.Abstraction;
using System.Diagnostics;
using System.Net;

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
    public AzTrace _trace;

    public void Dispose()
    {
        _chatService.Dispose();
    }

    public void Initialize(AgentConfig config)
    {

        _renderingStyle = config.RenderingStyle;
        _configRoot = config.ConfigurationRoot;
        SettingFile = Path.Combine(_configRoot, SettingFileName);
        _metricHelper = new MetricHelper();
        _trace  = new AzTrace();

        string tenantId = null;
        if (config.Context is not null)
        {
            config.Context.TryGetValue("tenant", out tenantId);
            config.Context.TryGetValue("subscription", out string subscriptionId);

            AgentInfo = new Dictionary<string, string>
            {
                ["Tenant"] = tenantId,
                ["Subscription"] = subscriptionId,
            };
        }

        _chatService = new AzPSChatService(config.IsInteractive, tenantId);
    }

    public IEnumerable<CommandBase> GetCommands() => null;
    public bool CanAcceptFeedback(UserAction action) => true;
    public void OnUserAction(UserActionPayload actionPayload) 
    {
        // Record user feedback
        if (actionPayload.Action == UserAction.Dislike)
        {
            var detailedFeedback = actionPayload.GetType().GetProperty("ShortFeedback").GetValue(actionPayload) + " : " + actionPayload.GetType().GetProperty("LongFeedback").GetValue(actionPayload);
            _trace.DetailedMessage = detailedFeedback.ToString();
        }
        // Record conversation
        if (actionPayload.Action == UserAction.Dislike || actionPayload.Action == UserAction.Like)
        {
            if (actionPayload.GetType().GetProperty("ShareConversation").GetValue(actionPayload).Equals(true))
            {
                // to record the last question/ answer
                // this needs the chatHistory of AzPSChatService to be public
            }
        }

        _trace.Handler = "Azure PowerShell";
        _trace.EventType = "Feedback";
        _trace.Command = actionPayload.Action.ToString();
        _metricHelper.LogTelemetry(AzPSChatService.Endpoint, _trace);
    }

    public async Task<bool> Chat(string input, IShell shell)
    {
        // Measure time spent
        var watch = Stopwatch.StartNew();
        _trace.StartTime = DateTime.Now;

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
            watch.Stop();
            _trace.EndTime = DateTime.Now;
            _trace.Duration = TimeSpan.FromTicks(watch.ElapsedTicks);
            _trace.Handler = "Azure PowerShell";
            _trace.EventType = "Question";
            _trace.Command = "Question";
            _trace.Question = input;
            _trace.Answer = streamingRender.AccumulatedContent;
            _metricHelper.LogTelemetry(AzPSChatService.Endpoint, _trace);
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
