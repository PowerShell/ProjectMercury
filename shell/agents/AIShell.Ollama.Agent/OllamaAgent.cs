using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AIShell.Abstraction;
using OllamaSharp;
using OllamaSharp.Models;

namespace AIShell.Ollama.Agent;

public sealed class OllamaAgent : ILLMAgent
{
    private bool _reloadSettings;
    private bool _isDisposed;
    private string _configRoot;
    private Settings _settings;
    private OllamaApiClient _client;
    private GenerateRequest _request;
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
    public Dictionary<string, string> LegalLinks { private set; get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ollama Docs"] = "https://github.com/ollama/ollama",
        ["Prerequisites"] = "https://github.com/PowerShell/AIShell/tree/main/shell/agents/AIShell.Ollama.Agent"
    };

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

        _request = new GenerateRequest()
        {
            Model = _settings.Model,
            Stream = _settings.Stream
        };

        _watcher = new FileSystemWatcher(_configRoot, SettingFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnSettingFileChange;
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

        // Self check settings Model and Endpoint
        if (!SelfCheck(host))
        {
            return false;
        }

        // Update request
        _request.Prompt = input;
        _request.Model = _settings.Model;
        _request.Stream = _settings.Stream;

        // Ollama client is created per chat with reloaded settings
        _client = new OllamaApiClient(_settings.Endpoint);

        try
        {
            if (_request.Stream)
            {
                using IStreamRender streamingRender = host.NewStreamRender(token);

                // Last stream response has context value
                GenerateDoneResponseStream ollamaLastStream = null;

                // Directly process the stream when no spinner is needed
                await foreach (var ollamaStream in _client.GenerateAsync(_request, token))
                {
                    // Update the render
                    streamingRender.Refresh(ollamaStream.Response);
                    if (ollamaStream.Done)
                    {
                       ollamaLastStream = (GenerateDoneResponseStream)ollamaStream;
                    }
                }

                // Update request context
                _request.Context = ollamaLastStream.Context;
            }
            else
            {
                // Build single response with spinner
                var ollamaResponse = await host.RunWithSpinnerAsync(
                    status: "Thinking ...",
                    func: async () => { return await _client.GenerateAsync(_request, token).StreamToEndAsync(); }
                ).ConfigureAwait(false);

                // Update request context
                _request.Context = ollamaResponse.Context;

                // Render the full response
                host.RenderFullResponse(ollamaResponse.Response);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore the cancellation exception.
        }
        catch (HttpRequestException e)
        {
            host.WriteErrorLine($"[{Name}]: {e.Message}");
            host.WriteErrorLine($"[{Name}]: Selected Model    : \"{_settings.Model}\"");
            host.WriteErrorLine($"[{Name}]: Selected Endpoint : \"{_settings.Endpoint}\"");
            host.WriteErrorLine($"[{Name}]: Configuration File: \"{SettingFile}\"");
            return false;
        }

        return true;
    }


    private void ReloadSettings()
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

    private bool SelfCheck(IHost host)
    {
        var settings = new (string settingValue, string settingName)[]
        {
            (_settings.Model, "Model"),
            (_settings.Endpoint, "Endpoint")
        };

        foreach (var (settingValue, settingName) in settings)
        {
            if (string.IsNullOrWhiteSpace(settingValue))
            {
                host.WriteErrorLine($"[{Name}]: {settingName} is undefined in the settings file: \"{SettingFile}\"");
                return false;
            }
        }

        return true;
    }

    private void NewExampleSettingFile()
    {
        string SampleContent = $$"""
        {
            // To use Ollama API service:
            // 1. Install Ollama:
            //      winget install Ollama.Ollama
            // 2. Start Ollama API server:
            //      ollama serve
            // 3. Install Ollama model:
            //      ollama pull phi3

            // Declare Ollama model
            "Model": "phi3",
            // Declare Ollama endpoint
            "Endpoint": "http://localhost:11434",
            // Enable Ollama streaming
            "Stream": false
        }
        """;
        File.WriteAllText(SettingFile, SampleContent, Encoding.UTF8);
    }
}
