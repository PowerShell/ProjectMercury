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

    private const string SettingFileName = "openai.agent.json";
    private bool _isInteractive;
    private bool _refreshSettings;
    private bool _isDisposed;
    private string _configRoot;
    private string _historyRoot;
    private bool _isFunctionCallingModel;
    private RenderingStyle _renderingStyle;
    private Settings _settings;
    private FileSystemWatcher _watcher;
    private ChatService _chatService;
    private TaskCompletionChat taskCompletionChat;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // This terminates any remaining processes used to run code.
        taskCompletionChat.CleanUpProcesses();

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
        _isFunctionCallingModel = ModelInfo.IsFunctionCallingModel(_settings.Active.ModelName);
        _chatService = new ChatService(_isInteractive, _historyRoot, _settings);

        Description = "An agent leverages GPTs that target OpenAI backends. Currently not ready to server queries.";
        GPT active = _settings.Active;
        if (active is not null)
        {
            Description = $"Active GTP: {active.Name}. {active.Description}";
        }

        _watcher = new FileSystemWatcher(_configRoot, SettingFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Created += OnSettingFileChange;
    }

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

        taskCompletionChat = new TaskCompletionChat(_isFunctionCallingModel, _chatService, host, token);
        await taskCompletionChat.StartTask(input, _renderingStyle);

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
  // Declare GPT instances.
  ""GPTs"": [
    // To use Azure OpenAI as the AI completion service:
    // - Set `Endpoint` to the endpoint of your Azure OpenAI service,
    //   or the endpoint to the Azure API Management service if you are using it as a gateway.
    // - Set `Deployment` to the deployment name of your Azure OpenAI service.
    // - Set `Key` to the access key of your Azure OpenAI service,
    //   or the key of the Azure API Management service if you are using it as a gateway.
    {{
      ""Name"": ""interpreter-gpt"",
      ""Description"": ""A GPT instance with expertise in PowerShell and Python scripting"",
      ""Endpoint"": ""{Utils.ShellCopilotEndpoint}"",
      ""Deployment"": ""gpt4"",
      ""ModelName"": ""gpt-4-0613"",   // required field to infer properties of the service, such as token limit.
      ""Key"": null,
      ""SystemPrompt"": ""You are Open Interpreter, a world-class programmer that can complete any goal by executing code. First, write a plan. **Always recap the plan between each code block** (you have extreme short-term memory loss, so you need to recap the plan between each message block to retain it). When you execute code, it will be executed **on the user's machine**. The user has given you **full and complete permission** to execute any code necessary to complete the task. Execute the code. If you want to send data between programming languages, save the data to a txt or json. You can access the internet. Run **any code** to achieve the goal, and if at first you don't succeed, try again and again. You can install new packages. When a user refers to a filename, they're likely referring to an existing file in the directory you're currently executing code in. Write messages to the user in Markdown. In general, try to **make plans** with as few steps as possible. As for actually executing code to carry out that plan, for *stateful* languages (like python, javascript, shell, but NOT for html which starts from 0 every time) **it's critical not to try to do everything in one code block.** You should try something, print information about it, then continue from there in tiny, informed steps. You will never get it on the first try, and attempting it in one go will often lead to errors you cant see. When giving python code add a blank line after an indented block is finished. When installing python libraries use powershell to pip install. You are capable of **any** task. Operating System: {Utils.OS}""
    }}              
    // To use the public OpenAI as the AI completion service:
    // - Ignore the `Endpoint` and `Deployment` keys.
    // - Set `Key` to be the OpenAI access token.
    // For example:
    /*
    {{
        ""Name"": ""python-ai"",
        ""Description"": ""A GPT instance that acts as an expert in python programming that can generate python code based on user's query."",
        ""ModelName"": ""gpt-4"",
        ""Key"": null,
        ""SystemPrompt"": ""example-system-prompt""
    }}
    */
  ],

  // Specify the GPT instance to use for user query.
  ""Active"": ""powershell-ai""
}}
";
        File.WriteAllText(SettingFile, SampleContent, Encoding.UTF8);
    }
}
