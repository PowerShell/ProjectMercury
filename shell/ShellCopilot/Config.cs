using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShellCopilot;

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

        ConfigFilePath = Path.Combine(Utils.AppConfigHome, $"{Utils.AppName}.config.json");
    }

    public ServiceConfig(List<AIModel> models, string activeModel)
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

    public List<AIModel> Models => _models;
    public string ActiveModel => _modelInUse.Name;

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
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            config = JsonSerializer.Deserialize<ServiceConfig>(stream, options);
        }

        if (config is null)
        {
            var key = new SecureString();
            key.AppendChar('a');
            key.AppendChar('b');

            // First time use. Populate the in-box powershell model.
            var pwshModel = new AIModel(
                name: "powershell-ai",
                description: "powershell ai model",
                systemPrompt: DefaultSystemPrompt,
                endpoint: "https://apim-my-openai.azure-api.net/",
                deployment: "gpt4",
                openAIModel: "gpt-4",
                key: key);

            config = new ServiceConfig(new() { pwshModel }, pwshModel.Name);
            WriteToConfigFile(config);
        }

        return config;
    }

    internal static void WriteToConfigFile(ServiceConfig config, bool ignoreApiKey = false)
    {
        if (!OperatingSystem.IsWindows() && !File.Exists(ConfigFilePath))
        {
            // Non-Windows platform file permissions must be set individually.
            // Windows platform file ACLs are inherited from containing directory.
            using (File.Create(ConfigFilePath)) { }
            Utils.SetFilePermissions(ConfigFilePath, isDirectory: false);
        }

        using FileStream stream = new FileStream(ConfigFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            TypeInfoResolver = new AIModelContractResolver(ignoreApiKey)
        };

        JsonSerializer.Serialize(stream, config, options);
    }
}
