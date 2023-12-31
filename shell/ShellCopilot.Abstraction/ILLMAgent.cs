
namespace ShellCopilot.Abstraction;

public enum RenderingStyle
{
    FullResponsePreferred,
    StreamingResponsePreferred,
}

public class AgentConfig
{
    public bool IsInteractive { set; get; }
    public string ConfigurationRoot { set; get; }
    public RenderingStyle RenderingStyle { set; get; }
}

public interface ILLMAgent : IDisposable
{
    string Name { get; }
    string Description { get; }
    string SettingFile { get; }

    void Initialize(AgentConfig config);
    Task<bool> Chat(string input, IShell shell);
    IEnumerable<CommandBase> GetCommands();
}

public interface IOrchestrator : ILLMAgent
{
    /// <summary>
    /// Find the most suitable agent to serve the prompt.
    /// </summary>
    /// <param name="prompt">User prompt to be send to the agent.</param>
    /// <param name="agents">List of descriptions for each of the agents</param>
    /// <returns>The index of the selected agent. Or -1 if none are suitable.</returns>
    Task<int> FindAgentForPrompt(string prompt, List<string> agents, CancellationToken token);
}

public interface ICodeAnalyzer : ILLMAgent
{
    Task<bool> AnalyzeCode(List<string> codeBlocks, IShell shell);
}
