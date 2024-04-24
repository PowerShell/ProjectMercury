using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Interpreter.Agent;

public sealed class InterpreterAgent : ILLMAgent
{
    public string Name => "interpreter";
    public string Description { private set; get; }
    public string SettingFile { private set; get; }

    private const string SettingFileName = "interpreter.agent.json";
    private bool _isInteractive;
    private bool _refreshSettings;
    private bool _isDisposed;
    private string _configRoot;
    private string _historyRoot;
    private RenderingStyle _renderingStyle;
    private Settings _settings;
    private FileSystemWatcher _watcher;
    private ChatService _chatService;
    private CodeExecutionService _executionService;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // This terminates any remaining processes used to run code.
        _taskCompletionChat.CleanUpProcesses();

        GC.SuppressFinalize(this);
        _watcher.Dispose();
        _isDisposed = true;
    }

    /// <inheritdoc/>
    public void Initialize(AgentConfig config)
    {
        _isInteractive = config.IsInteractive;
        _renderingStyle = config.RenderingStyle;
        _configRoot = config.ConfigurationRoot;

        SettingFile = Path.Combine(_configRoot, SettingFileName);
        if (!File.Exists(SettingFile))
        {
            NewExampleSettingFile();
        }

        _historyRoot = Path.Combine(_configRoot, "history");
        Directory.CreateDirectory(_historyRoot);

        _settings = ReadSettings();
        _executionService = new CodeExecutionService();
        _chatService = new ChatService(_isInteractive, _historyRoot, _settings, _executionService);

        Description = "An agent that specializes in completing code related tasks. This agent will write a plan, write code, execute code, and move on to the next step of the plan until the task is complete while correcting itself for any errors. Currently only supports PowerShell and Python.";

        _watcher = new FileSystemWatcher(_configRoot, SettingFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Created += OnSettingFileChange;
    }

    /// <inheritdoc/>
    public void RefreshChat()
    {
        _chatService.RefreshChat();
        _executionService.Terminate();
    }

    /// <inheritdoc/>
    public bool CanAcceptFeedback(UserAction action) => false;

    /// <inheritdoc/>
    public void OnUserAction(UserActionPayload actionPayload) { }

    /// <inheritdoc/>
    public IEnumerable<CommandBase> GetCommands() => null;

    /// <inheritdoc/>
    public async Task<bool> Chat(string input, IShell shell)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;
        bool checkPass = await SelfCheck(host, token);
        if (!checkPass)
        {
            host.MarkupWarningLine($"[[{Name}]]: Cannot serve the query due to the missing configuration. Please properly update the setting file.");
            return checkPass;
        }

        if (_refreshSettings)
        {
            _settings = ReadSettings();
            _chatService.RefreshSettings(_settings);
            _refreshSettings = false;
        }

        try
        {
            TaskCompletionChat _taskCompletionChat = new(_settings, _chatService, _executionService, host);
            await _taskCompletionChat.StartTask(input, _renderingStyle, token);
        }
        catch (OperationCanceledException)
        {
            // Ignore the exception.
        }

        return checkPass;
    }

    internal async Task<bool> SelfCheck(IHost host, CancellationToken token)
    {
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
        if (File.Exists(SettingFile))
        {
            using var stream = new FileStream(SettingFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            };
            var data = JsonSerializer.Deserialize<ConfigData>(stream, options);
            settings = new Settings(data);
        }

        return settings;
    }

    private void SaveSettings(Settings config)
    {
        using var stream = new FileStream(SettingFile, FileMode.Create, FileAccess.Write, FileShare.None);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        JsonSerializer.Serialize(stream, config.ToConfigData(), options);
    }

    private void OnSettingFileChange(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType is WatcherChangeTypes.Changed)
        {
            _refreshSettings = true;
        }
    }

    private void NewExampleSettingFile()
    {
        string SampleContent = $@"
{{
      ""Endpoint"" : ""{Utils.ShellCopilotEndpoint}"",
      ""Deployment"" : ""gpt4"",
      ""ModelName"" : ""gpt-4-0613"",   // required field to infer properties of the service, such as token limit.
      ""AutoExecution"" : false,
      ""DisplayErrors"" : true,
      ""Key"" : null

    // To use the public OpenAI as the AI completion service:
    // - Ignore the `Endpoint` and `Deployment` keys.
    // - Set `Key` to be the OpenAI access token.
    // Replace the above with the following:
    /*
      ""ModelName"": ""gpt-4-0613"",
      ""AutoExecution"": false,
      ""DisplayErrors"": false,
      ""Key"": null
    */
}}
";
        File.WriteAllText(SettingFile, SampleContent, Encoding.UTF8);
    }
}
