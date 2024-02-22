using System.Text;
using System.Text.Json;
using Azure.Identity;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Azure.CLI;

public sealed class AzCLIAgent : ILLMAgent
{
    public string Name => "az-cli";
    public string Description => "An AI assistant to provide Azure CLI scripts or commands for managing Azure resources and end-to-end scenarios that involve multiple Azure resources.";
    public Dictionary<string, string> AgentInfo { private set; get; } = null;
    public string SettingFile { private set; get; } = null;

    private const string SettingFileName = "az-cli.agent.json";
    private AzCLIChatService _chatService;
    private StringBuilder _text;

    public void Dispose()
    {
        _chatService?.Dispose();
    }

    public void Initialize(AgentConfig config)
    {
        _text = new StringBuilder();
        _chatService = new AzCLIChatService();

        if (config.Context is not null)
        {
            config.Context.TryGetValue("tenant", out string tenantId);
            config.Context.TryGetValue("subscription", out string subscriptionId);

            AgentInfo = new Dictionary<string, string>
            {
                ["Tenant"] = tenantId,
                ["Subscription"] = subscriptionId,
            };
        }

        SettingFile = Path.Combine(config.ConfigurationRoot, SettingFileName);
    }

    public IEnumerable<CommandBase> GetCommands() => null;

    public bool CanAcceptFeedback(UserAction action) => true;

    public void OnUserAction(UserActionPayload actionPayload)
    {
        // Send telemetry about the user action.
    }

    public async Task<bool> Chat(string input, IShell shell)
    {
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
                    host.MarkupErrorLine(azResponse.Error);
                    return true;
                }

                if (azResponse.Data.Count is 0)
                {
                    host.MarkupErrorLine("Sorry, no response received.");
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
                            .AppendLine(action.Example)
                            .AppendLine("```")
                            .AppendLine();
                    }

                    _text.AppendLine("Make sure to replace the placeholder values with your specific details.");
                }

                host.RenderFullResponse(_text.ToString());
            }
        }
        catch (RefreshTokenException ex)
        {
            Exception inner = ex.InnerException;
            if (inner is CredentialUnavailableException)
            {
                host.MarkupErrorLine($"Access token not available. Query cannot be served.");
                host.MarkupErrorLine($"The '{Name}' agent depends on the Azure CLI credential to acquire access token. Please run 'az login' from a command-line shell to setup account.");
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
