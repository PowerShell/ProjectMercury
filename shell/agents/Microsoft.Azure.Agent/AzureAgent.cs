using AIShell.Abstraction;

namespace Microsoft.Azure.Agent;

public sealed class AzCLIAgent : ILLMAgent
{
    public string Name => "Azure";
    public string Description => "This AI assistant can help generate Azure CLI and Azure PowerShell scripts or commands for managing Azure resources and end-to-end scenarios that involve multiple different Azure resources.";
    public string Company => "Microsoft";
    public List<string> SampleQueries => [
        "Create a VM with a public IP address",
        "How to create a web app?",
        "Backup an Azure SQL database to a storage container"
    ];
    public Dictionary<string, string> LegalLinks { private set; get; } = null;
    public string SettingFile { private set; get; } = null;

    private const string SettingFileName = "az.agent.json";

    private ChatSession _chatSession;
    private int _turnsLeft;

    public void Dispose()
    {
        _chatSession?.Dispose();
    }

    public void Initialize(AgentConfig config)
    {
        _chatSession = new ChatSession();
        _turnsLeft = int.MaxValue;

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
        // Refresh the chat session.
        _chatSession.Refresh(CancellationToken.None);
    }

    public IEnumerable<CommandBase> GetCommands() => null;
    public bool CanAcceptFeedback(UserAction action) => false;
    public void OnUserAction(UserActionPayload actionPayload) {}

    public async Task<bool> Chat(string input, IShell shell)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        if (_turnsLeft is 0)
        {
            host.WriteLine(@"\nYou have reached the limit of this conversation. Please run '/refresh' to start a new chat session.\n");
        }

        try
        {
            CopilotResponse copilotResponse = await host.RunWithSpinnerAsync(
                status: "Thinking ...",
                func: async context => await _chatSession.GetChatResponseAsync(context, input, token)
            ).ConfigureAwait(false);

            if (copilotResponse is not null)
            {
                host.RenderFullResponse(copilotResponse.Text);

                var conversationState = copilotResponse.ConversationState;
                string color = conversationState.TurnNumber switch
                {
                    < 10 => "green",
                    < 15 => "yellow",
                    _ => "red",
                };

                _turnsLeft = conversationState.TurnLimit - conversationState.TurnNumber;
                host.RenderDivider($"[{color}]{conversationState.TurnNumber} of {conversationState.TurnLimit} requests[/]");
            }
        }
        catch (Exception ex) when (ex is TokenRequestException or ConnectionDroppedException)
        {
            host.WriteErrorLine(ex.Message);
            host.WriteErrorLine("Please run '/refresh' to start a new chat session and try again.");
            return false;
        }

        return true;
    }
}
