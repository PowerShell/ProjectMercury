using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using ShellCopilot.Abstraction;

namespace ShellCopilot.OpenAI.Agent;

public sealed class OpenAIAgent : ILLMAgent
{
    public string Name => "openai-gpt";
    public string Description { private set; get; }
    public string SettingFile { private set; get; }

    private const string SettingFileName = "openai.agent.json";
    private bool _refreshSettings;
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

        SettingFile = Path.Combine(_configRoot, SettingFileName);
        if (!File.Exists(SettingFile))
        {
            NewExampleSettingFile();
        }

        _historyRoot = Path.Combine(_configRoot, "history");
        Directory.CreateDirectory(_historyRoot);

        _settings = ReadSettings();
        _chatService = new ChatService(_historyRoot, _settings);

        UpdateDescription();
        _watcher = new FileSystemWatcher(_configRoot, SettingFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Created += OnSettingFileChange;
    }

    public void RefreshChat()
    {
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

        if (_refreshSettings)
        {
            _settings = ReadSettings();
            _chatService.RefreshSettings(_settings);
            _refreshSettings = false;
        }

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
        GPT active = _settings.Active;
        Description = active is null
            ? "An agent leverages GPTs that target OpenAI backends. Currently not ready to serve queries."
            : $"Active GTP: {active.Name}. {active.Description}";
    }

    private async Task<bool> SelfCheck(IHost host, CancellationToken token)
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
  // Declare GPT instances.
  ""GPTs"": [
    // To use Azure OpenAI as the AI completion service:
    // - Set `Endpoint` to the endpoint of your Azure OpenAI service,
    //   or the endpoint to the Azure API Management service if you are using it as a gateway.
    // - Set `Deployment` to the deployment name of your Azure OpenAI service.
    // - Set `Key` to the access key of your Azure OpenAI service,
    //   or the key of the Azure API Management service if you are using it as a gateway.
    {{
      ""Name"": ""powershell-ai"",
      ""Description"": ""A GPT instance with expertise in PowerShell scripting and command line utilities."",
      ""Endpoint"": ""{Utils.ShellCopilotEndpoint}"",
      ""Deployment"": ""gpt4"",
      ""ModelName"": ""gpt-4-0314"",   // required field to infer properties of the service, such as token limit.
      ""Key"": null,
      ""SystemPrompt"": ""You are a helpful and friendly assistant with expertise in PowerShell scripting and command line.\nAssume user is using the operating system `{Utils.OS}` unless otherwise specified.\nPlease always respond in the markdown format and use the `code block` syntax to encapsulate any part in responses that is longer-format content such as code, YAML, JSON, and etc.""
    }},

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
