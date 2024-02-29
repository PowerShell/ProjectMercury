namespace ShellCopilot.Abstraction;

/// <summary>
/// Contract class for an application to warp around Shell Copilot.
/// </summary>
public class ShellWrapper
{
    private string _name;
    private string _banner;
    private string _version;
    private string _prompt;

    /// <summary>
    /// Name of the application to run from command line, e.g. 'az copilot'
    /// Set the name when you want to use a different configuration folder for Shell Copilot instead of the default.
    /// </summary>
    public string Name
    {
        set
        {
            // Normalize the empty string to be null.
            _name = string.IsNullOrEmpty(value) ? null : value;
        }

        get
        {
            return _name;
        }
    }

    /// <summary>
    /// Banner text to use.
    /// Shell Copilot will show the default banner if this is not set.
    /// </summary>
    public string Banner
    {
        set
        {
            // Normalize the empty string to be null.
            _banner = string.IsNullOrEmpty(value) ? null : value;
        }

        get
        {
            return _banner;
        }
    }

    /// <summary>
    /// Version to show.
    /// Shell Copilot will show the default version if this is not set.
    /// </summary>
    public string Version
    {
        set
        {
            // Normalize the empty string to be null.
            _version = string.IsNullOrEmpty(value) ? null : value;
        }

        get
        {
            return _version;
        }
    }

    /// <summary>
    /// The prompt text to use.
    /// Shell Copilot will use the default prompt if this is not set.
    /// </summary>
    public string Prompt
    {
        set
        {
            // Normalize the empty string to be null.
            _prompt = string.IsNullOrEmpty(value) ? null : value;
        }

        get
        {
            return _prompt;
        }
    }

    /// <summary>
    /// The default agent to use, which should be available along with Shell Copilot.
    /// This key is required to be set properly.
    /// </summary>
    public string Agent { set; get; }

    /// <summary>
    /// Context information that can be passed into the agent.
    /// </summary>
    public Dictionary<string, string> Context { set; get; }
}

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

    /// <summary>
    /// Sets and gets the context information for the agent that is passed into Shell Copilot.
    /// </summary>
    public Dictionary<string, string> Context { set; get; }
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
    /// Properties of the agent to be displayed to user.
    /// </summary>
    Dictionary<string, string> AgentInfo => null;

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

    /// <summary>
    /// Gets a value indicating whether the agent accepts a specific user action feedback.
    /// </summary>
    /// <param name="action">The user action.</param>
    bool CanAcceptFeedback(UserAction action);

    /// <summary>
    /// A user action was taken against the last response from this agent.
    /// </summary>
    /// <param name="action">Type of the action.</param>
    /// <param name="actionPayload"></param>
    void OnUserAction(UserActionPayload actionPayload);
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
