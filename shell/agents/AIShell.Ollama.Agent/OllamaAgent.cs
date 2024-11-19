using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AIShell.Abstraction;

namespace AIShell.Ollama.Agent;

public sealed class OllamaAgent : ILLMAgent
{
    private bool _reloadSettings;
    private bool _isDisposed;
    private string _configRoot;
    private Settings _settings;
    private FileSystemWatcher _watcher;

    /// <summary>
    /// The name of setting file
    /// </summary>
    private const string SettingFileName = "ollama.config.json";

    /// <summary>
    /// Gets the settings.
    /// </summary>
    internal Settings Settings => _settings;

    /// <summary>
    /// The name of the agent
    /// </summary>
    public string Name => "ollama";

    /// <summary>
    /// The description of the agent to be shown at start up
    /// </summary>
    public string Description => "This is an AI assistant to interact with a language model running locally by utilizing the Ollama CLI tool. Be sure to follow all prerequisites in https://github.com/PowerShell/AIShell/tree/main/shell/agents/AIShell.Ollama.Agent";

    /// <summary>
    /// This is the company added to /like and /dislike verbiage for who the telemetry helps.
    /// </summary>
    public string Company => "Microsoft";

    /// <summary>
    /// These are samples that are shown at start up for good questions to ask the agent
    /// </summary>
    public List<string> SampleQueries => [
        "How do I list files in a given directory?"
    ];

    /// <summary>
    /// These are any optional legal/additional information links you want to provide at start up
    /// </summary>
    public Dictionary<string, string> LegalLinks { private set; get; }

    /// <summary>
    /// This is the chat service to call the API from
    /// </summary>
    private OllamaChatService _chatService;

    /// <summary>
    /// Dispose method to clean up the unmanaged resource of the chatService
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        _chatService?.Dispose();
        _watcher.Dispose();
        _isDisposed = true;
    }

    /// <summary>
    /// Initializing function for the class when the shell registers an agent
    /// </summary>
    /// <param name="config">Agent configuration for any configuration file and other settings</param>
    public void Initialize(AgentConfig config)
    {
        _configRoot = config.ConfigurationRoot;

        SettingFile = Path.Combine(_configRoot, SettingFileName);
        _settings = ReadSettings();

        if (_settings is null)
        {
            // Create the setting file with examples to serve as a template for user to update.
            NewExampleSettingFile();
            _settings = ReadSettings();
        }

        _chatService = new OllamaChatService(_settings);

        _watcher = new FileSystemWatcher(_configRoot, SettingFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnSettingFileChange;

        LegalLinks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ollama Docs"] = "https://github.com/ollama/ollama",
            ["Prerequisites"] = "https://github.com/PowerShell/AIShell/tree/main/shell/agents/AIShell.Ollama.Agent"
        };
    }

    /// <summary>
    /// Get commands that an agent can register to the shell when being loaded.
    /// </summary>
    public IEnumerable<CommandBase> GetCommands() => null;

    /// <summary>
    /// Gets the path to the setting file of the agent.
    /// </summary>
    public string SettingFile { private set; get; }

    /// <summary>
    /// Gets a value indicating whether the agent accepts a specific user action feedback.
    /// </summary>
    /// <param name="action">The user action.</param>
    public bool CanAcceptFeedback(UserAction action) => false;

    /// <summary>
    /// A user action was taken against the last response from this agent.
    /// </summary>
    /// <param name="action">Type of the action.</param>
    /// <param name="actionPayload"></param>
    public void OnUserAction(UserActionPayload actionPayload) {}

    /// <summary>
    /// Refresh the current chat by starting a new chat session.
    /// This method allows an agent to reset chat states, interact with user for authentication, print welcome message, and more.
    /// </summary>
    public Task RefreshChatAsync(IShell shell, bool force)
    {
        if (force)
        {
            // Reload the setting file if needed.
            ReloadSettings();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Main chat function that takes the users input and passes it to the LLM and renders it.
    /// </summary>
    /// <param name="input">The user input from the chat experience.</param>
    /// <param name="shell">The shell that provides host functionality.</param>
    /// <returns>Task Boolean that indicates whether the query was served by the agent.</returns>
    public async Task<bool> ChatAsync(string input, IShell shell)
    {
        // Get the shell host
        IHost host = shell.Host;

        // get the cancellation token
        CancellationToken token = shell.CancellationToken;

        // Reload the setting file if needed.
        ReloadSettings();

        if (Process.GetProcessesByName("ollama").Length is 0)
        {
            host.MarkupWarningLine($"[[{Name}]]: Please be sure the Ollama is installed and server is running. Check all the prerequisites in the README of this agent are met.");
            return false;
        }

        try
        {
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
        catch (HttpRequestException)
        {
            host.MarkupWarningLine($"[[{Name}]]: Cannot serve the query due to the endpoint or model misconfiguration. Please properly update the setting file.");
            return false;
        }

        return true;
    }

    internal void ReloadSettings()
    {
        if (_reloadSettings)
        {
            _reloadSettings = false;
            var settings = ReadSettings();
            if (settings is null)
            {
                return;
            }

            _settings = settings;
            _chatService.RefreshSettings(_settings);
        }
    }

    private Settings ReadSettings()
    {
        Settings settings = null;
        FileInfo file = new(SettingFile);

        if (file.Exists)
        {
            try
            {
                using var stream = file.OpenRead();
                var data = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.ConfigData);
                settings = new Settings(data);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Parsing settings from '{SettingFile}' failed with the following error: {e.Message}", e);
            }
        }

        return settings;
    }

    private void OnSettingFileChange(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType is WatcherChangeTypes.Changed)
        {
            _reloadSettings = true;
        }
    }

    private void NewExampleSettingFile()
    {
        string SampleContent = $$"""
        {
            /*
            To use Ollama API service:

            1. Install Ollama:
                winget install Ollama.Ollama

            2. Start Ollama API server:
                ollama serve

            3. Install Ollama model:
                ollama pull phi3
            */

            // Declare Ollama model
            "Model": "phi3",
            // Declare Ollama endpoint
            "Endpoint": "http://localhost:11434"
        }
        """;
        File.WriteAllText(SettingFile, SampleContent, Encoding.UTF8);
    }
}
