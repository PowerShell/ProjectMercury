using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Interpreter.Agent;

public sealed class InterpreterAgent : ILLMAgent
{
    public string Name => "interpreter-gpt";
    public string Description { private set; get; }
    public string SettingFile { private set; get; }

    private const string SettingFileName = "interpreter.agent.json";
    private bool _isInteractive;
    private bool _refreshSettings;
    private bool _isDisposed;
    private string _configRoot;
    private string _historyRoot;
    private bool _isFunctionCallingModel;
    private bool _autoExecution;
    private bool _displayErrors;
    private RenderingStyle _renderingStyle;
    private Settings _settings;
    private FileSystemWatcher _watcher;
    private ChatService _chatService;
    private TaskCompletionChat _taskCompletionChat;

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
        // while (!System.Diagnostics.Debugger.IsAttached)
        // {
        //     System.Threading.Thread.Sleep(200);
        // }
        // System.Diagnostics.Debugger.Break();

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
        _isFunctionCallingModel = ModelInfo.IsFunctionCallingModel(_settings.ModelName);
        _autoExecution = _settings.AutoExecution;
        _displayErrors = _settings.DisplayErrors;
        _chatService = new ChatService(_isInteractive, _historyRoot, _settings);

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
        _taskCompletionChat?.CleanUpProcesses();
    }

    /// <inheritdoc/>
    public bool CanAcceptFeedback(UserAction action) => false;

    /// <inheritdoc/>
    public void OnUserAction(UserActionPayload actionPayload) { }
    /// <inheritdoc/>
    public IEnumerable<CommandBase> GetCommands()
    {
        return null;
    }

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
            _taskCompletionChat = new TaskCompletionChat(_isFunctionCallingModel, 
                                                         _autoExecution,
                                                         _displayErrors,
                                                         _chatService,
                                                         host);
            await _taskCompletionChat.StartTask(input, _renderingStyle, token);
        }
        catch (OperationCanceledException)
        {
            // Ignore the except
        }
        catch (ArgumentException ex)
        {
            host.MarkupWarningLine($"[[{Name}]]: {ex.Message}");
        }
        finally
        {
            _taskCompletionChat.CleanUpProcesses();
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

    private static string GetWarningBasedOnFinishReason(CompletionsFinishReason reason)
    {
        if (reason.Equals(CompletionsFinishReason.TokenLimitReached))
        {
            return "The response was incomplete as the max token limit was exhausted.";
        }

        if (reason.Equals(CompletionsFinishReason.ContentFiltered))
        {
            return "The response was truncated as it was identified as potentially sensitive per content moderation policies.";
        }

        return null;
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
            var data = System.Text.Json.JsonSerializer.Deserialize<ConfigData>(stream, options);
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

        System.Text.Json.JsonSerializer.Serialize(stream, config.ToConfigData(), options);
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
        // ""You are Open Interpreter, a world-class programmer that can complete any goal by executing code. First, write a plan. **Always recap the plan between each code block** (you have extreme short-term memory loss, so you need to recap the plan between each message block to retain it). When you execute code, it will be executed **on the user's machine**. The user has given you **full and complete permission** to execute any code necessary to complete the task. Execute the code. If you want to send data between programming languages, save the data to a txt or json. You can access the internet. Run **any code** to achieve the goal, and if at first you don't succeed, try again and again. You can install new packages. When a user refers to a filename, they're likely referring to an existing file in the directory you're currently executing code in. Write messages to the user in Markdown. In general, try to **make plans** with as few steps as possible. As for actually executing code to carry out that plan, for *stateful* languages (like python, javascript, shell, but NOT for html which starts from 0 every time) **it's critical not to try to do everything in one code block.** You should try something, print information about it, then continue from there in tiny, informed steps. You will never get it on the first try, and attempting it in one go will often lead to errors you cant see. When giving python code add a blank line after an indented block is finished. When installing python libraries use powershell to pip install. You are capable of **any** task. Operating System: {Utils.OS}""
        File.WriteAllText(SettingFile, SampleContent, Encoding.UTF8);
    }
}
