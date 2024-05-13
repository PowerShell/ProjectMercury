using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using AISH.Abstraction;

namespace AISH.Azure.CLI;

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

    public void RefreshChat()
    {
        // Reset the history so the subsequent chat can start fresh.
        _chatService.ChatHistory.Clear();
    }

    public IEnumerable<CommandBase> GetCommands() => null;

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

    public async Task<bool> Chat(string input, IShell shell)
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
                    host.WriteErrorLine(azResponse.Error);
                    return true;
                }

                if (azResponse.Data.Count is 0)
                {
                    host.WriteErrorLine("Sorry, no response received.");
                    return true;
                }

                var data = azResponse.Data[0];
                var history = _chatService.ChatHistory;
                while (history.Count > Utils.HistoryCount - 2)
                {
                    history.RemoveAt(0);
                }
                history.Add(new ChatMessage { Role = "user", Content = input });
                history.Add(new ChatMessage { Role = "assistant", Content = JsonSerializer.Serialize(data, Utils.JsonOptions) });

                _text.Clear();
                _text.AppendLine(data.Description).AppendLine();

                if (data.CommandSet.Count > 0)
                {
                    _text.AppendLine("Action step(s):").AppendLine();

                    for (int i = 0; i < data.CommandSet.Count; i++)
                    {
                        Action action = data.CommandSet[i];
                        _text.AppendLine($"{i+1}. {action.Reason}")
                            .AppendLine()
                            .AppendLine("```sh")
                            .AppendLine($"# {action.Reason}")
                            .AppendLine(action.Example)
                            .AppendLine("```")
                            .AppendLine();
                    }

                    _text.AppendLine("Make sure to replace the placeholder values with your specific details.");
                }

                host.RenderFullResponse(_text.ToString());

                // Measure time spent
                _watch.Stop();

                if (!MetricHelper.TelemetryOptOut) 
                {
                    // TODO: extract into RecordQuestionTelemetry() : RecordTelemetry()
                    var EndTime = DateTime.Now;
                    var Duration = TimeSpan.FromTicks(_watch.ElapsedTicks);

                    // Append last Q&A history in HistoryMessage
                    _historyForTelemetry.AddLast(new HistoryMessage("user", input, _chatService.CorrelationID));
                    _historyForTelemetry.AddLast(new HistoryMessage("assistant", _text.ToString(), _chatService.CorrelationID));

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
}
