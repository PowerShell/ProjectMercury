using System.Text.Json;
using Azure.AI.OpenAI;
using ShellCopilot.Abstraction;

namespace ShellCopilot.OpenAI.Agent;

public sealed class OpenAIAgent : ILLMAgent
{
    public string Name => "openai-gpt";
    public string Description { private set; get; }
    public string SettingFile { private set; get; }

    private const string SettingFileName = "openai.agent.json";
    private bool _reloadSettings;
    private bool _isDisposed;
    private string _configRoot;
    private string _historyRoot;
    private Settings _settings;
    private FileSystemWatcher _watcher;
    private ChatService _chatService;

    /// <summary>
    /// Gets the settings.
    /// </summary>
    internal Settings Settings => _settings;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void Initialize(AgentConfig config)
    {
        _configRoot = config.ConfigurationRoot;
        _historyRoot = Path.Combine(_configRoot, "history");
        if (!Directory.Exists(_historyRoot))
        {
            Directory.CreateDirectory(_historyRoot);
        }

        SettingFile = Path.Combine(_configRoot, SettingFileName);
        _settings = ReadSettings();
        _chatService = new ChatService(_historyRoot, _settings);

        UpdateDescription();
        _watcher = new FileSystemWatcher(_configRoot, SettingFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnSettingFileChange;
    }

    public void RefreshChat()
    {
        // Reload the setting file if needed.
        ReloadSettings();
        // Reset the history so the subsequent chat can start fresh.
        _chatService.ChatHistory.Clear();
    }

    /// <inheritdoc/>
    public IEnumerable<CommandBase> GetCommands() => [new GPTCommand(this)];

    /// <inheritdoc/>
    public bool CanAcceptFeedback(UserAction action) => false;

    /// <inheritdoc/>
    public void OnUserAction(UserActionPayload actionPayload) {}

    /// <inheritdoc/>
    public async Task<bool> Chat(string input, IShell shell)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;

        // Reload the setting file if needed.
        ReloadSettings();

        bool checkPass = await SelfCheck(host, token);
        if (!checkPass)
        {
            host.MarkupWarningLine($"[[{Name}]]: Cannot serve the query due to the missing configuration. Please properly update the setting file.");
            return checkPass;
        }

        string responseContent = null;
        StreamingResponse<StreamingChatCompletionsUpdate> response = await host.RunWithSpinnerAsync(
            () => _chatService.GetStreamingChatResponseAsync(input, token)
        ).ConfigureAwait(false);

        if (response is not null)
        {
            using var streamingRender = host.NewStreamRender(token);

            try
            {
                await foreach (StreamingChatCompletionsUpdate chatUpdate in response)
                {
                    if (string.IsNullOrEmpty(chatUpdate.ContentUpdate))
                    {
                        continue;
                    }

                    streamingRender.Refresh(chatUpdate.ContentUpdate);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore the cancellation exception.
            }

            responseContent = streamingRender.AccumulatedContent;
        }

        _chatService.AddResponseToHistory(responseContent);

        return checkPass;
    }

    internal void UpdateDescription()
    {
        const string DefaultDescription = "This agent is designed to provide a flexible platform for interacting with OpenAI services (Azure OpenAI or the public OpenAI) through one or more customly defined GPT instances. Learn more at https://aka.ms/aish/openai\n";

        if (_settings is null || _settings.GPTs.Count is 0)
        {
            Description = $"""
            {DefaultDescription}
            The agent is currently not ready to serve queries, because there is no GPT defined. Please follow the steps below to configure the setting file properly before using this agent:
              1. Run '/agent config' to open the setting file.
              2. Define the GPT(s). See the example at
                 {Utils.SettingHelpLink}
              3. Save and close the setting file.
              4. Run '/refresh' to apply the new settings.
            """;

            return;
        }

        if (_settings.Active is null)
        {
            Description = $"""
            {DefaultDescription}
            Multiple GPTs are defined but the active GPT is not specified. You will be prompted to choose from the available GPTs when sending the first query. Or, if you want to set the active GPT in configuration, please follow the steps below:
              1. Run '/agent config' to open the setting file.
              2. Set the 'Active' key. See the example at
                 {Utils.SettingHelpLink}
              3. Save and close the setting file
              4. Run '/refresh' to apply the new settings.
            """;

            return;
        }

        GPT active = _settings.Active;
        Description = $"Active GTP: {active.Name}. {active.Description}";
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
            UpdateDescription();
        }
    }

    private async Task<bool> SelfCheck(IHost host, CancellationToken token)
    {
        if (_settings is null)
        {
            return false;
        }

        bool checkPass = await _settings.SelfCheck(host, token);

        if (_settings.Dirty)
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
                SaveSettings(_settings);
                _settings.MarkClean();
            }
            finally
            {
                _watcher.EnableRaisingEvents = true;
            }
        }

        return checkPass;
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

    private void SaveSettings(Settings config)
    {
        using var stream = new FileStream(SettingFile, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, config.ToConfigData(), SourceGenerationContext.Default.ConfigData);
    }

    private void OnSettingFileChange(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType is WatcherChangeTypes.Changed)
        {
            _reloadSettings = true;
        }
    }
}
