
namespace ShellCopilot.Abstraction;

public enum RenderingStyle
{
    /// <summary>
    /// Inform the agent that it's preferred to wait for a response to complete and render the full response.
    /// </summary>
    FullResponsePreferred,

    /// <summary>
    /// Inform the agent that it's preferred to use stream response and render new chunks as they arrive.
    /// </summary>
    StreamingResponsePreferred,
}

public class AgentConfig
{
    /// <summary>
    /// Sets and gets whether the shell is being used interactively.
    /// </summary>
    public bool IsInteractive { set; get; }

    /// <summary>
    /// Sets and gets the root directory for configuration file.
    /// </summary>
    public string ConfigurationRoot { set; get; }

    /// <summary>
    /// Sets and gets the preferred rendering style.
    /// </summary>
    public RenderingStyle RenderingStyle { set; get; }
}

public interface ILLMAgent : IDisposable
{
    /// <summary>
    /// Gets name of the agent.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets description of the agent.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the path to the setting file of the agent.
    /// </summary>
    string SettingFile { get; }

    /// <summary>
    /// Initializes the agent with the provided configuration.
    /// </summary>
    /// <param name="config">The configuration settings for the agent.</param>
    void Initialize(AgentConfig config);

    /// <summary>
    /// Initiates a chat with the AI, using the provided input and shell.
    /// </summary>
    /// <param name="input">The query message for the AI.</param>
    /// <param name="shell">The interface for interacting with the shell.</param>
    /// <returns>A task whose result contains a boolean indicating whether the query was successfully served.</returns>
    Task<bool> Chat(string input, IShell shell);

    /// <summary>
    /// Retrieves the collection of commands to be registered to the shell for the agent.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="CommandBase"/> objects representing the available commands.</returns>
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
    /// <summary>
    /// Analyze code blocks for any security concerns.
    /// </summary>
    /// <param name="codeBlocks"></param>
    /// <param name="shell"></param>
    /// <returns></returns>
    Task<bool> AnalyzeCode(List<string> codeBlocks, IShell shell);
}
