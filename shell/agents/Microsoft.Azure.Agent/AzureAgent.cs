using System.Diagnostics;
using System.Text;
using AIShell.Abstraction;
using Microsoft.Identity.Client;

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

    internal string UserValuePrompt { set; get; }
    internal UserValueStore ValueStore { get; } = new();
    internal ArgumentPlaceholder ArgPlaceholder { set; get; }

    private const string SettingFileName = "az.agent.json";
    private const string HorizontalRule = "\n---\n";

    private int _turnsLeft;
    private StringBuilder _buffer;
    private ChatSession _chatSession;

    public void Dispose()
    {
        _chatSession?.Dispose();
    }

    public void Initialize(AgentConfig config)
    {
        _turnsLeft = int.MaxValue;
        _buffer = new StringBuilder();
        _chatSession = new ChatSession();

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

    public IEnumerable<CommandBase> GetCommands() => [new ReplaceCommand(this)];
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

        string query = UserValuePrompt is null ? input : $"{UserValuePrompt}\n{HorizontalRule}\n{input}";
        UserValuePrompt = null;

        try
        {
            CopilotResponse copilotResponse = await host.RunWithSpinnerAsync(
                status: "Thinking ...",
                func: async context => await _chatSession.GetChatResponseAsync(query, context, token)
            ).ConfigureAwait(false);

            if (copilotResponse is null)
            {
                // User cancelled the operation.
                return true;
            }

            if (copilotResponse.ChunkReader is null)
            {
                string text = copilotResponse.Text;

                // Process response from CLI handler specially to support parameter injection.
                if (CopilotActivity.CLIHandlerTopic.Equals(copilotResponse.TopicName, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Replace("~~~", "```");
                    ResponseData data = ParseCLIHandlerResponse(text, shell);
                    if (data is null)
                    {
                        // No code blocks in the response, or there is no placeholders in its code blocks.
                        ArgPlaceholder = null;
                        host.RenderFullResponse(text);
                    }
                    else
                    {
                        string answer = GenerateAnswer(input, data);
                        host.RenderFullResponse(answer);
                    }
                }
                else
                {
                    ArgPlaceholder = null;
                    host.RenderFullResponse(text);
                }
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
            _turnsLeft = conversationState.TurnLimit - conversationState.TurnNumber;
            if (_turnsLeft <= 5)
            {
                string message = _turnsLeft switch
                {
                    1 => $"[yellow]{_turnsLeft} request left[/]",
                    0 => $"[red]{_turnsLeft} request left[/]",
                    _ => $"[yellow]{_turnsLeft} requests left[/]",
                };

                host.RenderDivider(message, DividerAlignment.Right);
                if (_turnsLeft is 0)
                {
                    host.WriteLine("\nYou've reached the maximum length of a conversation. To continue, please run '/refresh' to start a new conversation.\n");
                }
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

    private static ResponseData ParseCLIHandlerResponse(string text, IShell shell)
    {
        List<CodeBlock> codeBlocks = shell.ExtractCodeBlocks(text, out List<SourceInfo> sourceInfos);
        if (codeBlocks is null || codeBlocks.Count is 0)
        {
            return null;
        }

        Debug.Assert(codeBlocks.Count == sourceInfos.Count, "There should be 1-to-1 mapping for code block and its source info.");

        HashSet<string> phSet = null;
        List<PlaceholderItem> placeholders = null;
        List<CommandItem> commands = new(capacity: codeBlocks.Count);

        for (int i = 0; i < codeBlocks.Count; i++)
        {
            string script = codeBlocks[i].Code;
            commands.Add(new CommandItem { SourceInfo = sourceInfos[i], Script = script });

            // placeholder is in the `<xxx>` form.
            int start = -1;
            for (int k = 0; k < script.Length; k++)
            {
                char c = script[k];
                if (c is '<')
                {
                    start = k;
                }
                else if (c is '>')
                {
                    placeholders ??= [];
                    phSet ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string ph = script[start..(k+1)];
                    if (phSet.Add(ph))
                    {
                        placeholders.Add(new PlaceholderItem { Name = ph, Desc = ph, Type = "string" });
                    }

                    start = -1;
                }
            }
        }

        return new ResponseData { Text = text, CommandSet = commands, PlaceholderSet = placeholders };
    }

    internal string GenerateAnswer(string input, ResponseData data)
    {
        _buffer.Clear();
        string text = data.Text;

        // We keep 'ArgPlaceholder' unchanged when it's re-generating in '/replace' with only partial placeholders replaced.
        if (!ReferenceEquals(ArgPlaceholder?.ResponseData, data) || data.PlaceholderSet is null)
        {
            ArgPlaceholder?.DataRetriever?.Dispose();
            ArgPlaceholder = null;
        }

        if (data.PlaceholderSet?.Count > 0)
        {
            // Create the data retriever for the placeholders ASAP, so it gets
            // more time to run in background.
            ArgPlaceholder ??= new ArgumentPlaceholder(input, data);
        }

        int index = 0;
        foreach (CommandItem item in data.CommandSet)
        {
            // Replace the pseudo values with the real values.
            string script = ValueStore.ReplacePseudoValues(item.Script);
            if (!ReferenceEquals(script, item.Script))
            {
                _buffer.Append(text.AsSpan(index, item.SourceInfo.Start - index));
                _buffer.Append(script);
                index = item.SourceInfo.End + 1;
            }
        }

        if (index is 0)
        {
            return text;
        }

        if (index < text.Length)
        {
            _buffer.Append(text.AsSpan(index, text.Length - index));
        }

        return _buffer.ToString();
    }
}
