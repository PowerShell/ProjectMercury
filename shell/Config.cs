using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

using Azure;
using Azure.Core;
using Azure.AI.OpenAI;
using SharpToken;

namespace Shell;

internal class Utils
{
    internal static readonly bool IsWindows;
    internal static readonly string OS;

    static Utils()
    {
        IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        string rid = RuntimeInformation.RuntimeIdentifier;
        int index = rid.IndexOf('-');
        OS = index is -1 ? rid : rid.Substring(0, index);
    }

    internal static string GetDataFromSecureString(SecureString secureString)
    {
        nint ptr = Marshal.SecureStringToBSTR(secureString);
        try
        {
            return Marshal.PtrToStringBSTR(ptr);
        }
        finally
        {
            Marshal.ZeroFreeBSTR(ptr);
        }
    }

    internal static SecureString ConvertDataToSecureString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var ss = new SecureString();
        foreach (char c in text)
        {
            ss.AppendChar(c);
        }

        return ss;
    }
}

internal class ChatResponse
{
    internal ChatResponse(ChatChoice choice)
    {
        Content = choice.Message.Content;
        FinishReason = choice.FinishReason;
    }

    internal string Content { get; }
    internal string FinishReason { get; }
}

internal class BackendService
{
    private const string ApimAuthorizationHeader = "Ocp-Apim-Subscription-Key";
    private const string ApimGatewayDomain = ".azure-api.net";
    private const string AzureOpenAIDomain = ".openai.azure.com";

    // TODO: Maybe make this a configuration entry?
    private const int MaxResponseToken = 250;

    private readonly ServiceConfig _config;
    private OpenAIClient _client;
    private AIModel _activeModel;
    private List<ChatMessage> _chatHistory;

    internal BackendService()
    {
        _config = ServiceConfig.ReadFromConfigFile();
        _chatHistory = new List<ChatMessage>();

        _activeModel = _config.GetModelInUse();
        _client = NewOpenAIClient(_activeModel);
    }

    internal ServiceConfig Configuration => _config;

    private void RefreshOpenAIClientAsNeeded(AIModel modelInUse)
    {
        if (string.Equals(_activeModel.Name, modelInUse.Name))
        {
            return;
        }

        _activeModel = modelInUse;
        _client = NewOpenAIClient(modelInUse);
    }

    private OpenAIClient NewOpenAIClient(AIModel activeModel)
    {
        var clientOptions = new OpenAIClientOptions() { RetryPolicy = new ApimRetryPolicy() };
        bool isApimEndpoint = activeModel.Endpoint.TrimEnd('/').EndsWith(ApimGatewayDomain);

        if (isApimEndpoint && activeModel.Key is not null)
        {
            string userkey = Utils.GetDataFromSecureString(activeModel.Key);
            clientOptions.AddPolicy(
                new UserKeyPolicy(
                    new AzureKeyCredential(userkey),
                    ApimAuthorizationHeader),
                HttpPipelinePosition.PerRetry
            );
        }

        string azOpenAIApiKey = isApimEndpoint
            ? "placeholder-api-key"
            : Utils.GetDataFromSecureString(activeModel.Key);

        OpenAIClient client = new(
            new Uri(activeModel.Endpoint),
            new AzureKeyCredential(azOpenAIApiKey),
            clientOptions);

        return client;
    }

    private int CountTokenForMessages(IEnumerable<ChatMessage> messages)
    {
        var openAIModel = _activeModel.SimpleOpenAIModelName;
        var encoding = GptEncoding.GetEncodingForModel(openAIModel);

        // For reference, see the 'num_tokens_from_messages' function from
        // https://github.com/openai/openai-cookbook/blob/main/examples/How_to_format_inputs_to_ChatGPT_models.ipynb
        (int tokenPerMessage, int tokenPerName) = openAIModel switch
        {
            "gpt-4" => (3, 1),
            "gpt-3.5-turbo" or "gpt-35-turbo" => (4, -1),
            _ => throw new NotSupportedException($"Token count is not implemented for the OpenAI model '{_activeModel.OpenAIModel}'."),
        };

        int tokenNumber = 0;
        foreach (ChatMessage message in messages)
        {
            // TODO: the 'name' field of the prompt message cannot be supported yet due to the limitation of Azure.AI.OpenAI package.
            // But it should be supported eventually.
            tokenNumber += tokenPerMessage;
            tokenNumber += encoding.Encode(message.Role.Label).Count;
            tokenNumber += encoding.Encode(message.Content).Count;
        }

        // Every reply is primed with <|start|>assistant<|message|>, which takes 3 tokens.
        tokenNumber += 3;
        return tokenNumber;
    }

    private void ReduceChatHistoryAsNeeded(List<ChatMessage> history, ChatMessage input)
    {
        int totalTokens = CountTokenForMessages(Enumerable.Repeat(input, 1));
        int tokenLimit = _activeModel.TokenLimit;

        if (totalTokens + MaxResponseToken >= tokenLimit)
        {
            var message = $"The input is too long to get a proper response without exceeding the token limit ({tokenLimit}).\nPlease reduce the input and try again.";
            throw new InvalidOperationException(message);
        }

        history.Add(input);
        totalTokens = CountTokenForMessages(history);

        while (totalTokens + MaxResponseToken >= tokenLimit)
        {
            history.RemoveAt(0);
            totalTokens = CountTokenForMessages(history);
        }
    }

    private ChatCompletionsOptions PrepareForChatCompletion(string input, bool insertToHistory)
    {
        var modelInUse = _config.GetModelInUse();
        RefreshOpenAIClientAsNeeded(modelInUse);

        ChatCompletionsOptions chatOptions = new()
        {
            ChoicesPerPrompt = 1,
            Temperature = (float)0.7,
            MaxTokens = MaxResponseToken,
        };

        List<ChatMessage> history = insertToHistory ? _chatHistory : new List<ChatMessage>();
        if (history.Count is 0)
        {
            history.Add(new ChatMessage(ChatRole.System, modelInUse.SystemPrompt));
        }

        ReduceChatHistoryAsNeeded(history, new ChatMessage(ChatRole.User, input));
        foreach (ChatMessage message in history)
        {
            chatOptions.Messages.Add(message);
        }

        return chatOptions;
    }

    public ChatResponse GetChatResponse(string input, bool insertToHistory = true)
    {
        ChatCompletionsOptions chatOptions = PrepareForChatCompletion(input, insertToHistory);
        Response<ChatCompletions> response = _client.GetChatCompletions(_activeModel.Deployment, chatOptions);
        return new ChatResponse(response.Value.Choices[0]);
    }

    public async Task<ChatResponse> GetChatResponseAsync(string input, bool insertToHistory = true, CancellationToken cancellationToken = default)
    {
        ChatCompletionsOptions chatOptions = PrepareForChatCompletion(input, insertToHistory);
        Response<ChatCompletions> response = await _client.GetChatCompletionsAsync(_activeModel.Deployment, chatOptions, cancellationToken);
        return new ChatResponse(response.Value.Choices[0]);
    }
}

internal class ServiceConfig
{
    private static readonly string ConfigFilePath;
    private static readonly string DefaultSystemPrompt;

    private readonly object _syncObj;
    private readonly List<AIModel> _models;
    private readonly Dictionary<string, AIModel> _modelDict;
    private AIModel _modelInUse;

    static ServiceConfig()
    {
        DefaultSystemPrompt = @$"
You are an AI assistant with expertise in PowerShell, Azure, and the command line.
Assume user is using the operating system ""{Utils.OS}"" unless otherwise specified.
You are helpful, creative, clever, and very friendly.
You always respond in the markdown format.
You use the ""code blocks"" syntax from markdown to encapsulate any part in responses that's longer-format content such as code, poem, lyrics, etc.";

        string locationPath = Utils.IsWindows
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("HOME");

        string appFolderPath = Path.Combine(locationPath, "ai");
        ConfigFilePath = Path.Combine(appFolderPath, "ai.json");

        if (!Directory.Exists(appFolderPath))
        {
            Directory.CreateDirectory(appFolderPath);
            if (Utils.IsWindows)
            {
                SetDirectoryACLs(appFolderPath);
            }
            else
            {
                SetFilePermissions(appFolderPath, isDirectory: true);
            }
        }
    }

    internal ServiceConfig(List<AIModel> models, string activeModel)
    {
        _syncObj = new object();
        _models = models ?? new List<AIModel>();
        _modelDict = new Dictionary<string, AIModel>(capacity: _models.Count, StringComparer.OrdinalIgnoreCase);

        var dupModels = new List<string>();
        foreach (var model in _models)
        {
            // TODO: need to validate to make sure all mandatory fields have expected values.
            // Also, need to populate the missing optional values with default values.
            if (!_modelDict.TryAdd(model.Name, model))
            {
                dupModels.Add(model.Name);
            }
        }

        if (dupModels.Count > 0)
        {
            string message = $"The passed-in model list contains the following duplicate models: {string.Join(',', dupModels)}.";
            throw new ArgumentException(message, nameof(models));
        }

        if (!_modelDict.TryGetValue(activeModel, out _modelInUse))
        {
            string message = $"The passed-in active model '{activeModel}' doesn't exist.";
            throw new ArgumentException(message, nameof(activeModel));
        }
    }

    internal List<AIModel> Models => _models;
    internal string ActiveModel => _modelInUse.Name;

    internal AIModel GetModelInUse()
    {
        return _modelInUse;
    }

    internal AIModel GetModelByName(string name)
    {
        lock (_syncObj)
        {
            if (_modelDict.TryGetValue(name, out AIModel model))
            {
                return model;
            }
        }

        return null;
    }

    internal void UseModel(string name)
    {
        lock (_syncObj)
        {
            if (string.Equals(_modelInUse.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_modelDict.TryGetValue(name, out AIModel model))
            {
                var message = $"A model with the name '{name}' doesn't exist.";
                throw new ArgumentException(message, nameof(name));
            }

            _modelInUse = model;
            WriteToConfigFile(this);
        }
    }

    internal void AddModel(AIModel model)
    {
        // TODO: need to validate to make sure all mandatory fields have expected values.
        // Also, need to populate the missing optional values with default values.
        lock (_syncObj)
        {
            if (!_modelDict.TryAdd(model.Name, model))
            {
                var message = $"A model with the name '{model.Name}' has already been registered.";
                throw new InvalidOperationException(message);
            }

            _models.Add(model);
            WriteToConfigFile(this);
        }
    }

    internal void RemoveModel(string name)
    {
        lock (_syncObj)
        {
            if (string.Equals(_modelInUse.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                var message = $"The specified model '{name}' cannot be removed because it's actively in use. Change the active model and try again.";
                throw new InvalidOperationException(message);
            }

            if (!_modelDict.Remove(name))
            {
                var message = $"A model with the name '{name}' doesn't exist.";
                throw new ArgumentException(message, nameof(name));
            }

            int index = 0;
            for (; index < _models.Count; index++)
            {
                if (_models[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            _models.RemoveAt(index);
            WriteToConfigFile(this);
        }
    }

    internal static ServiceConfig ReadFromConfigFile()
    {
        ServiceConfig config = null;
        if (File.Exists(ConfigFilePath))
        {
            using var stream = new FileStream(ConfigFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            config = JsonSerializer.Deserialize<ServiceConfig>(stream);
        }

        if (config is null)
        {
            // First time use. Populate the in-box powershell model.
            var pwshModel = new AIModel(
                name: "powershell-ai",
                description: "powershell ai model",
                systemPrompt: DefaultSystemPrompt,
                endpoint: "https://apim-my-openai.azure-api.net/",
                deployment: "gpt4",
                openAIModel: "gpt-4",
                key: null);

            config = new ServiceConfig(new() { pwshModel }, pwshModel.Name);
            WriteToConfigFile(config);
        }

        return config;
    }

    internal static void WriteToConfigFile(ServiceConfig config)
    {
        if (!Utils.IsWindows && !File.Exists(ConfigFilePath))
        {
            // Non-Windows platform file permissions must be set individually.
            // Windows platform file ACLs are inherited from containing directory.
            using (File.Create(ConfigFilePath)) { }
            SetFilePermissions(ConfigFilePath, isDirectory: false);
        }

        using FileStream stream = new FileStream(ConfigFilePath, FileMode.Open, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, config);
    }

    private static void SetDirectoryACLs(string directoryPath)
    {
        // Windows platform.

        // For Windows, file permissions are set to FullAccess for current user account only.
        // SetAccessRule method applies to this directory.
        var dirSecurity = new DirectorySecurity();
        dirSecurity.SetAccessRule(
            new FileSystemAccessRule(
                identity: WindowsIdentity.GetCurrent().User,
                type: AccessControlType.Allow,
                fileSystemRights: FileSystemRights.FullControl,
                inheritanceFlags: InheritanceFlags.None,
                propagationFlags: PropagationFlags.None));

        // AddAccessRule method applies to child directories and files.
        dirSecurity.AddAccessRule(
            new FileSystemAccessRule(
            identity: WindowsIdentity.GetCurrent().User,
            fileSystemRights: FileSystemRights.FullControl,
            type: AccessControlType.Allow,
            inheritanceFlags: InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
            propagationFlags: PropagationFlags.InheritOnly));

        // Set access rule protections.
        dirSecurity.SetAccessRuleProtection(
            isProtected: true,
            preserveInheritance: false);

        // Set directory owner.
        dirSecurity.SetOwner(WindowsIdentity.GetCurrent().User);

        // Apply new rules.
        System.IO.FileSystemAclExtensions.SetAccessControl(
            directoryInfo: new DirectoryInfo(directoryPath),
            directorySecurity: dirSecurity);
    }

    private static void SetFilePermissions(
        string path,
        bool isDirectory)
    {
        // Non-Windows platforms.

        // Set directory permissions to current user only.
        /*
        Current user is user owner.
        Current user is group owner.
        Permission for user dir owner:      rwx    (execute for directories only)
        Permission for user file owner:     rw-    (no file execute)
        Permissions for group owner:        ---    (no access)
        Permissions for others:             ---    (no access)
        */
        string argument = isDirectory ? 
            string.Format(CultureInfo.InvariantCulture, @"u=rwx,g=---,o=--- {0}", path) :
            string.Format(CultureInfo.InvariantCulture, @"u=rw-,g=---,o=--- {0}", path);

        ProcessStartInfo startInfo = new("chmod", argument);
        Process.Start(startInfo).WaitForExit();
    }
}

public enum TrustLevel
{
    Private,
    Public,
}

public class AIModel
{
    // For reference, see https://platform.openai.com/docs/models
    private static readonly Dictionary<string, int> ModelToTokenLimitMapping = new Dictionary<string, int>
    {
        ["gpt-4"] = 8192,
        ["gpt-4-32k"] = 32768,
        ["gpt-3.5-turbo"] = 4096,
        ["gpt-3.5-turbo-16k"] = 16384,
        ["gpt-35-turbo"] = 4096,
        ["gpt-35-turbo-16k"] = 16384,
    };

    private string _name;
    private string _prompt;
    private string _endpoint;
    private string _deployment;
    private string _openAIModel;
    private string _simpleOpenAIModelName;
    private int _tokenLimit;

    internal AIModel(
        string name,
        string description,
        string systemPrompt,
        string endpoint,
        string deployment,
        string openAIModel,
        SecureString key,
        TrustLevel trustLevel = TrustLevel.Public)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(systemPrompt);
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        ArgumentException.ThrowIfNullOrEmpty(deployment);
        ArgumentException.ThrowIfNullOrEmpty(openAIModel);

        _name = name;
        _prompt = systemPrompt;
        _endpoint = endpoint;
        _deployment = deployment;
        _openAIModel = openAIModel.ToLowerInvariant();
        InferSettingsFromOpenAIModel();

        Description = description;        
        Key = key;
        TrustLevel = trustLevel;
    }

    private void InferSettingsFromOpenAIModel()
    {
        if (!ModelToTokenLimitMapping.TryGetValue(_openAIModel, out _tokenLimit))
        {
            var message = $"The specified '{_openAIModel}' is not a supported Azure OpenAI chat completion model.";
            throw new ArgumentException(message, nameof(_openAIModel));
        }

        // For reference: https://github.com/openai/tiktoken/blob/5d970c1100d3210b42497203d6b5c1e30cfda6cb/tiktoken/model.py#L7
        // The fixed consumption of tokens per message is different between gpt-3.5 and gpt-4, so we need to simplify the name
        // to indicate which one it is.
        _simpleOpenAIModelName = _openAIModel.StartsWith("gpt-4-", StringComparison.Ordinal)
            ? "gpt-4"
            : _openAIModel.StartsWith("gpt-3.5-turbo-", StringComparison.Ordinal)
              || _openAIModel.StartsWith("gpt-35-turbo-", StringComparison.Ordinal)
                ? "gpt-35-turbo"
                : _openAIModel;
    }

    public string Name => _name;

    public string SystemPrompt
    {
        get { return _prompt; }

        internal set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            _prompt = value;
        }
    }

    public string Endpoint
    {
        get { return _endpoint; }

        internal set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            _endpoint = value;
        }
    }

    public string Deployment
    {
        get { return _deployment; }

        internal set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            _deployment = value;
        }
    }

    public string OpenAIModel
    {
        get { return _openAIModel; }

        internal set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            _openAIModel = value.ToLowerInvariant();
            InferSettingsFromOpenAIModel();
        }
    }

    internal string SimpleOpenAIModelName => _simpleOpenAIModelName;
    internal int TokenLimit => _tokenLimit;

    public string Description { get; internal set; }    
    public SecureString Key { get; internal set; }
    public TrustLevel TrustLevel { get; internal set; }
}
