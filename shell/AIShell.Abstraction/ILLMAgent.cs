namespace AIShell.Abstraction;

/// <summary>
/// Contract class for an application to warp around AIShell.
/// </summary>
public class ShellWrapper
{
    string _version;
    string _description;

    /// <summary>
    /// Name of the application to run from command line, e.g. 'az copilot'. Required key.
    /// It's used to setup the configuration folder of AIShell.
    /// </summary>
    public string Name { set; get; }

    /// <summary>
    /// Banner text to be displayed at the startup of AIShell. Required key.
    /// </summary>
    public string Banner { set; get; }

    /// <summary>
    /// Version to be displayed at the startup of AIShell. Optional key.
    /// The version of AIShell will be used if this key is not specified.
    /// </summary>
    public string Version
    {
        // Normalize the empty string to be null.
        set => _version = string.IsNullOrEmpty(value) ? null : value;
        get => _version;
    }

    /// <summary>
    /// The description to be displayed at the startup of AIShell. Optional key.
    /// The default description of the chosen agent will be used if this key is not specified.
    /// </summary>
    public string Description
    {
        // Normalize the empty string to be null.
        set => _description = string.IsNullOrEmpty(value) ? null : value;
        get => _description;
    }

    /// <summary>
    /// The prompt text to use. Required key.
    /// </summary>
    public string Prompt { set; get; }

    /// <summary>
    /// The default agent to use, which should be available along with AIShell. Required key.
    /// </summary>
    public string Agent { set; get; }

    /// <summary>
    /// Context information that can be passed into the agent. Optional key.
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
    /// Sets and gets the context information for the agent that is passed into AIShell.
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
    /// Gets the company or vendor of this agent.
    /// </summary>
    string Company => null;

    /// <summary>
    /// Gets some sample queries.
    /// </summary>
    List<string> SampleQueries => null;

    /// <summary>
    /// Gets URL links for legal terms, such as "Terms of use", "Privacy statement".
    /// </summary>
    Dictionary<string, string> LegalLinks => null;

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
    /// Refresh the current chat or force starting a new chat session.
    /// This method allows an agent to reset chat states, refresh token or settings, interact with user for authentication, print welcome message, and more.
    /// </summary>
    /// <param name="shell">The interface for interacting with the shell.</param>
    /// <param name="force">Whether or not to force creating a new chat session.</param>
    Task RefreshChatAsync(IShell shell, bool force);

    /// <summary>
    /// Initiates a chat with the AI, using the provided input and shell.
    /// </summary>
    /// <param name="input">The query message for the AI.</param>
    /// <param name="shell">The interface for interacting with the shell.</param>
    /// <returns>A task whose result contains a boolean indicating whether the query was successfully served.</returns>
    Task<bool> ChatAsync(string input, IShell shell);

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
