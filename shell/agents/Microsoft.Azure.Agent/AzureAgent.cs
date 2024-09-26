using AIShell.Abstraction;

namespace Microsoft.Azure.Agent;

public sealed class AzureAgent : ILLMAgent
{
    public string Name => "Azure";
    public string Company => "Microsoft";
    public List<string> SampleQueries => [
        "Create a VM with a public IP address",
        "How to create a web app?",
        "Backup an Azure SQL database to a storage container"
    ];

    public string Description { private set; get; }
    public Dictionary<string, string> LegalLinks { private set; get; }
    public string SettingFile { private set; get; }

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

        Description = "This AI assistant can generate Azure CLI and Azure PowerShell commands for managing Azure resources, answer questions, and provides information tailored to your specific Azure environment.";
        LegalLinks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Terms"] = "https://aka.ms/TermsofUseCopilot",
            ["Privacy"] = "https://aka.ms/privacy",
            ["FAQ"] = "https://aka.ms/CopilotforAzureClientToolsFAQ",
            ["Transparency"] = "https://aka.ms/CopilotAzCLIPSTransparency",
        };

        SettingFile = Path.Combine(config.ConfigurationRoot, SettingFileName);
    }

    public IEnumerable<CommandBase> GetCommands() => null;
    public bool CanAcceptFeedback(UserAction action) => false;
    public void OnUserAction(UserActionPayload actionPayload) {}

    public async Task RefreshChatAsync(IShell shell)
    {
        // Refresh the chat session.
        await _chatSession.RefreshAsync(shell.Host, shell.CancellationToken);
    }

    public async Task<bool> ChatAsync(string input, IShell shell)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        if (_turnsLeft is 0)
        {
            host.WriteLine("\nSorry, you've reached the maximum length of a conversation. Please run '/refresh' to start a new conversation.\n");
            return true;
        }

        try
        {
            CopilotResponse copilotResponse = await host.RunWithSpinnerAsync(
                status: "Thinking ...",
                func: async context => await _chatSession.GetChatResponseAsync(input, context, token)
            ).ConfigureAwait(false);

            if (copilotResponse is null)
            {
                // User cancelled the operation.
                return true;
            }

            if (copilotResponse.ChunkReader is null)
            {
                host.RenderFullResponse(copilotResponse.Text);
            }
            else
            {
                try
                {
                    using var streamingRender = host.NewStreamRender(token);
                    CopilotActivity prevActivity = null;

                    while (true)
                    {
                        CopilotActivity activity = copilotResponse.ChunkReader.ReadChunk(token);
                        if (activity is null)
                        {
                            prevActivity.ExtractMetadata(out string[] suggestion, out ConversationState state);
                            copilotResponse.SuggestedUserResponses = suggestion;
                            copilotResponse.ConversationState = state;
                            break;
                        }

                        int start = prevActivity is null ? 0 : prevActivity.Text.Length;
                        streamingRender.Refresh(activity.Text[start..]);
                        prevActivity = activity;
                    }
                }
                catch (OperationCanceledException)
                {
                    // User cancelled the operation.
                    // TODO: we may need to notify azure copilot somehow about the cancellation.
                }
            }

            var conversationState = copilotResponse.ConversationState;
            string color = conversationState.TurnNumber switch
            {
                < 10 => "green",
                < 15 => "yellow",
                _ => "red",
            };

            _turnsLeft = conversationState.TurnLimit - conversationState.TurnNumber;
            host.RenderDivider($"[{color}]{conversationState.TurnNumber} of {conversationState.TurnLimit} requests[/]", DividerAlignment.Right);
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
