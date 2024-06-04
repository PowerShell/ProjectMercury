using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Ollama.Agent;

public sealed class OllamaAgent : ILLMAgent
{
    // Name of the agent
    public string Name => "ollama";

    // Description displayed on start up
    public string Description => "This is an AI assistant that utilizes Ollama"; // TODO prerequistates for running this agent

    // This is the company added to /like and /dislike verbage for who the telemetry helps.
    public string Company => "Microsoft";

    // These are samples that are shown at start up
    public List<string> SampleQueries => [
        "How do I list files in a given directory?"
    ];

    // These are any legal/additional information links you want to provide at start up
    public Dictionary<string, string> LegalLinks { private set; get; }
    
    private OllamaChatService _chatService;

    // Text to be rendered at the end.
    private StringBuilder _text; 

    public void Dispose()
    {
        _chatService?.Dispose();
    }

    public void Initialize(AgentConfig config)
    {
        _text = new StringBuilder();
        _chatService = new OllamaChatService();

        LegalLinks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ollama Docs"] = "https://github.com/ollama/ollama",
        };

    }

    // 
    public IEnumerable<CommandBase> GetCommands() => null;

    public string SettingFile { private set; get; } = null;

    public void RefreshChat() {}

    public bool CanAcceptFeedback(UserAction action) => false;
    
    public void OnUserAction(UserActionPayload actionPayload) {}

    // Main chat functions
    public async Task<bool> Chat(string input, IShell shell)
    {
        // Get the shell host
        IHost host = shell.Host; 

        // get the cancellation token
        CancellationToken token = shell.CancellationToken; 

        try
        {
            if (Utils.IsCliToolInstalled("ollama") && Utils.isPortOpen(11434)){
                host.RenderFullResponse("Please ensure you have done all the prerequistates before using this");
                return false;
            }
            else {
                ResponseData ollamaResponse = await host.RunWithSpinnerAsync(
                    status: "Thinking ...",
                    func: async context => await _chatService.GetChatResponseAsync(context, input, token)
                ).ConfigureAwait(false);

                if (ollamaResponse is not null)
                {
                    // render the content
                    host.RenderFullResponse(ollamaResponse.response); 
                }
            }
            
        }
        catch (OperationCanceledException e)
        {
            _text.AppendLine(e.ToString());

            host.RenderFullResponse(_text.ToString());
            
            return false;
        }
       
        return true;
    }
    
}
