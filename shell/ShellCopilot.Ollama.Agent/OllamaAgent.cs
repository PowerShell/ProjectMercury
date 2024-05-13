using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Ollama.Agent;

public sealed class OllamaAgent : ILLMAgent
{
    public string Name => "ollama";
    public string Description => "This is an AI assistant that utilizes Ollama";
    public string Company => "Microsoft";
    public List<string> SampleQueries => [
        "What color is the sky"
    ];
    public Dictionary<string, string> LegalLinks { private set; get; } = null;

    private readonly Stopwatch _watch = new();

    private OllamaChatService _chatService;
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

    public IEnumerable<CommandBase> GetCommands() => null;

    public string SettingFile { private set; get; } = null;

    public void RefreshChat() {}

    public bool CanAcceptFeedback(UserAction action) => false;
    
    public void OnUserAction(UserActionPayload actionPayload){}

    public async Task<bool> Chat(string input, IShell shell)
    {
        // Measure time spent
        _watch.Restart();
        var startTime = DateTime.Now;

        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        try
        {
            OllamaResponse ollama_Response = await host.RunWithSpinnerAsync(
                status: "Thinking ...",
                func: async context => await _chatService.GetChatResponseAsync(context, input, token)
            ).ConfigureAwait(false);

            if (ollama_Response is not null)
            {
                if (ollama_Response.Error is not null)
                {
                    host.WriteErrorLine(ollama_Response.Error);
                    return true;
                }

                if (ollama_Response.Data.Count is 0)
                {
                    host.WriteErrorLine("Sorry, no response received.");
                    return true;
                }

                var data = ollama_Response.Data[0];

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

            }
        }
        catch (Exception e)
        {
            
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
