using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using AIShell.Abstraction;

namespace AIShell.Azure.CLI;

public sealed class AzCLIAgent : ILLMAgent
{
    public string Name => "az-cli";
    public string Description => "This AI assistant can help generate Azure CLI scripts or commands for managing Azure resources and end-to-end scenarios that involve multiple different Azure resources.";
    public string Company => "Microsoft";
    public List<string> SampleQueries => [
        "Create a VM with a public IP address",
        "How to create a web app?",
        "Backup an Azure SQL database to a storage container"
    ];
    public Dictionary<string, string> LegalLinks { private set; get; } = null;
    public string SettingFile { private set; get; } = null;
    internal ArgumentPlaceholder ArgPlaceholder { set; get; }
    internal UserValueStore ValueStore { get; } = new();

    private const string SettingFileName = "az-cli.agent.json";
    private readonly Stopwatch _watch = new();

    private AzCLIChatService _chatService;
    private StringBuilder _text;
    private MetricHelper _metricHelper;
    private LinkedList<HistoryMessage> _historyForTelemetry;

    public void Dispose()
    {
        _chatService?.Dispose();
    }

    public void Initialize(AgentConfig config)
    {
        _text = new StringBuilder();
        _chatService = new AzCLIChatService();
        _historyForTelemetry = [];
        _metricHelper = new MetricHelper(AzCLIChatService.Endpoint);

        LegalLinks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Terms"] = "https://aka.ms/TermsofUseCopilot",
            ["Privacy"] = "https://aka.ms/privacy",
            ["FAQ"] = "https://aka.ms/CopilotforAzureClientToolsFAQ",
            ["Transparency"] = "https://aka.ms/CopilotAzCLIPSTransparency",
        };

        SettingFile = Path.Combine(config.ConfigurationRoot, SettingFileName);
    }

    public IEnumerable<CommandBase> GetCommands() => [new ReplaceCommand(this)];

    public bool CanAcceptFeedback(UserAction action) => !MetricHelper.TelemetryOptOut;

    public void OnUserAction(UserActionPayload actionPayload)
    {
        // Send telemetry about the user action.
        // DisLike Action
        string DetailedMessage = null;
        LinkedList<HistoryMessage> history = null;
        if (actionPayload.Action == UserAction.Dislike)
        {
            DislikePayload dislikePayload = (DislikePayload)actionPayload;
            DetailedMessage = string.Format("{0} | {1}", dislikePayload.ShortFeedback, dislikePayload.LongFeedback);
            if (dislikePayload.ShareConversation)
            {
                history = _historyForTelemetry;
            }
            else
            {
                _historyForTelemetry.Clear();
            }
        }
        // Like Action
        else if (actionPayload.Action == UserAction.Like)
        {
            LikePayload likePayload = (LikePayload)actionPayload;
            if (likePayload.ShareConversation)
            {
                history = _historyForTelemetry;
            }
            else
            {
                _historyForTelemetry.Clear();
            }
        }

        _metricHelper.LogTelemetry(
            new AzTrace()
            {
                Command = actionPayload.Action.ToString(),
                CorrelationID = _chatService.CorrelationID,
                EventType = "Feedback",
                Handler = "Azure CLI",
                DetailedMessage = DetailedMessage,
                HistoryMessage = history
            });
    }

    public Task RefreshChatAsync(IShell shell, bool force)
    {
        // Reset the history so the subsequent chat can start fresh.
        _chatService.ChatHistory.Clear();
        ArgPlaceholder = null;
        ValueStore.Clear();

        return Task.CompletedTask;
    }

    public async Task<bool> ChatAsync(string input, IShell shell)
    {
        // Measure time spent
        _watch.Restart();
        var startTime = DateTime.Now;

        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        try
        {
            AzCliResponse azResponse = await host.RunWithSpinnerAsync(
                status: "Thinking ...",
                func: async context => await _chatService.GetChatResponseAsync(context, input, token)
            ).ConfigureAwait(false);

            if (azResponse is not null)
            {
                if (azResponse.Error is not null)
                {
                    host.WriteLine($"\n{azResponse.Error}\n");
                    return true;
                }

                ResponseData data = azResponse.Data;
                AddMessageToHistory(
                    JsonSerializer.Serialize(data, Utils.JsonOptions),
                    fromUser: false);

                string answer = GenerateAnswer(input, data);
                host.RenderFullResponse(answer);

                // Measure time spent
                _watch.Stop();

                if (!MetricHelper.TelemetryOptOut) 
                {
                    // TODO: extract into RecordQuestionTelemetry() : RecordTelemetry()
                    var EndTime = DateTime.Now;
                    var Duration = TimeSpan.FromTicks(_watch.ElapsedTicks);

                    // Append last Q&A history in HistoryMessage
                    _historyForTelemetry.AddLast(new HistoryMessage("user", input, _chatService.CorrelationID));
                    _historyForTelemetry.AddLast(new HistoryMessage("assistant", answer, _chatService.CorrelationID));

                    _metricHelper.LogTelemetry(
                        new AzTrace()
                        {
                            CorrelationID = _chatService.CorrelationID,
                            Duration = Duration,
                            EndTime = EndTime,
                            EventType = "Question",
                            Handler = "Azure CLI",
                            StartTime = startTime
                        });
                }
            }
        }
        catch (RefreshTokenException ex)
        {
            Exception inner = ex.InnerException;
            if (inner is CredentialUnavailableException)
            {
                host.WriteErrorLine($"Access token not available. Query cannot be served.");
                host.WriteErrorLine($"The '{Name}' agent depends on the Azure CLI credential to acquire access token. Please run 'az login' from a command-line shell to setup account.");
            }
            else
            {
                host.WriteErrorLine($"Failed to get the access token. {inner.Message}");
            }

            return false;
        }
        finally
        {
            // Stop the watch in case of early return or exception.
            _watch.Stop();
        }

        return true;
    }

    internal string GenerateAnswer(string input, ResponseData data)
    {
        _text.Clear();
        _text.Append(data.Description).Append("\n\n");

        // We keep 'ArgPlaceholder' unchanged when it's re-generating in '/replace' with only partial placeholders replaced.
        if (!ReferenceEquals(ArgPlaceholder?.ResponseData, data) || data.PlaceholderSet is null)
        {
            ArgPlaceholder?.DataRetriever?.Dispose();
            ArgPlaceholder = null;
        }

        if (data.CommandSet.Count > 0)
        {
            // AzCLI handler incorrectly include pseudo values in the placeholder set, so we need to filter them out.
            UserValueStore.FilterOutPseudoValues(data);
            if (data.PlaceholderSet?.Count > 0)
            {
                // Create the data retriever for the placeholders ASAP, so it gets
                // more time to run in background.
                ArgPlaceholder ??= new ArgumentPlaceholder(input, data);
            }

            for (int i = 0; i < data.CommandSet.Count; i++)
            {
                CommandItem action = data.CommandSet[i];
                // Replace the pseudo values with the real values.
                string script = ValueStore.ReplacePseudoValues(action.Script);

                _text.Append($"{i+1}. {action.Desc}")
                    .Append("\n\n")
                    .Append("```sh\n")
                    .Append($"# {action.Desc}\n")
                    .Append(script).Append('\n')
                    .Append("```\n\n");
            }

            if (ArgPlaceholder is not null)
            {
                _text.Append("Please provide values for the following placeholder variables:\n\n");

                for (int i = 0; i < data.PlaceholderSet.Count; i++)
                {
                    PlaceholderItem item = data.PlaceholderSet[i];
                    _text.Append($"- `{item.Name}`: {item.Desc}\n");
                }

                _text.Append("\nRun `/replace` to get assistance in placeholder replacement.\n");
            }
        }

        return _text.ToString();
    }

    internal void AddMessageToHistory(string message, bool fromUser)
    {
        if (!string.IsNullOrEmpty(message))
        {
            var history = _chatService.ChatHistory;
            while (history.Count > Utils.HistoryCount - 1)
            {
                history.RemoveAt(0);
            }

            history.Add(new ChatMessage()
                {
                    Role = fromUser ? "user" : "assistant",
                    Content = message
                });
        }
    }
}
