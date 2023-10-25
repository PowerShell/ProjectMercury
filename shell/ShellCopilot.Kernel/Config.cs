using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShellCopilot.Kernel;

internal class Configuration
{
    private static readonly string ConfigFilePath;
    private static readonly string DefaultSystemPrompt;

    private readonly object _syncObj;
    private readonly List<AiModel> _models;
    private readonly Dictionary<string, AiModel> _modelDict;
    private AiModel _modelInUse;

    static Configuration()
    {
        DefaultSystemPrompt = @$"
You are an AI assistant with expertise in PowerShell, Azure, and the command line.
Assume user is using the operating system ""{Utils.OS}"" unless otherwise specified.
You are helpful, creative, clever, and very friendly.
You always respond in the markdown format.
You use the ""code blocks"" syntax from markdown to encapsulate any part in responses that's longer-format content such as code, poem, lyrics, etc.";

        ConfigFilePath = Path.Combine(Utils.AppConfigHome, $"{Utils.AppName}.config.json");
    }

    public Configuration(List<AiModel> models, string activeModel)
    {
        _syncObj = new object();
        _models = models ?? new List<AiModel>();
        _modelDict = new Dictionary<string, AiModel>(capacity: _models.Count, StringComparer.OrdinalIgnoreCase);

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

    public List<AiModel> Models => _models;
    public string ActiveModel => _modelInUse.Name;

    internal AiModel GetModelInUse()
    {
        return _modelInUse;
    }

    internal AiModel GetModelByName(string name)
    {
        lock (_syncObj)
        {
            if (_modelDict.TryGetValue(name, out AiModel model))
            {
                return model;
            }
        }

        return null;
    }

    internal void UseModel(string name, bool keyRequired, CancellationToken cancellationToken)
    {
        lock (_syncObj)
        {
            if (!_modelDict.TryGetValue(name, out AiModel model))
            {
                var message = $"A model with the name '{name}' doesn't exist.";
                throw new ArgumentException(message, nameof(name));
            }

            bool modelUpdated = false;
            if (model.Key is null)
            {
                modelUpdated = model.RequestForKey(keyRequired, cancellationToken);
                if (keyRequired && !modelUpdated)
                {
                    var message = $"Model '{name}' cannot be made active because its access key is missing.";
                    throw new InvalidOperationException(message);
                }
            }

            if (!modelUpdated && _modelInUse.Name.Equals(model.Name, StringComparison.Ordinal))
            {
                // Target is the same model with no update.
                return;
            }

            _modelInUse = model;
            WriteToConfigFile(this);
        }
    }

    internal void AddModels(params AiModel[] models)
    {
        lock (_syncObj)
        {
            foreach (AiModel model in models)
            {
                // TODO: need to validate to make sure all mandatory fields have expected values.
                // Also, need to populate the missing optional values with default values.
                if (!_modelDict.TryAdd(model.Name, model))
                {
                    var message = $"A model with the name '{model.Name}' has already been registered.";
                    throw new InvalidOperationException(message);
                }
            }

            _models.AddRange(models);
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

    internal static Configuration ReadFromConfigFile()
    {
        Configuration config = null;
        if (File.Exists(ConfigFilePath))
        {
            using var stream = new FileStream(ConfigFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            config = JsonSerializer.Deserialize<Configuration>(stream, options);
        }

        if (config is null)
        {
            // First time use. Populate the in-box powershell model.
            var pwshModel = new AiModel(
                name: "powershell-ai",
                description: "powershell ai model",
                systemPrompt: DefaultSystemPrompt,
                endpoint: Utils.ShellCopilotEndpoint,
                deployment: "gpt4",
                openAIModel: "gpt-4-0314",
                key: null);

            config = new Configuration(new() { pwshModel }, pwshModel.Name);
            WriteToConfigFile(config);
        }

        return config;
    }

    internal static void WriteToConfigFile(Configuration config)
    {
        if (!OperatingSystem.IsWindows() && !File.Exists(ConfigFilePath))
        {
            // Non-Windows platform file permissions must be set individually.
            // Windows platform file ACLs are inherited from containing directory.
            using (File.Create(ConfigFilePath)) { }
            Utils.SetFilePermissions(ConfigFilePath, isDirectory: false);
        }

        using var stream = new FileStream(ConfigFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        JsonSerializer.Serialize(stream, config, options);
    }
}

internal static class ServiceConfigExtensions
{
    internal static void ListAllModels(this Configuration config)
    {
        ConsoleRender.RenderTable(
            config.Models,
            new[] {
                new RenderElement<AiModel>(nameof(AiModel.Name)),
                new RenderElement<AiModel>(label: "Active", m => m.Name == config.ActiveModel ? true.ToString() : string.Empty),
                new RenderElement<AiModel>(nameof(AiModel.TrustLevel)),
                new RenderElement<AiModel>(nameof(AiModel.Description)),
                new RenderElement<AiModel>(label: "Key", m => m.Key is null ? "[red]missing[/]" : "[green]saved[/]"),
            });
    }

    internal static void ShowOneModel(this Configuration config, string name)
    {
        var model = config.GetModelByName(name);
        ConsoleRender.RenderList(
            model,
            new[]
            {
                new RenderElement<AiModel>(nameof(AiModel.Name)),
                new RenderElement<AiModel>(nameof(AiModel.Description)),
                new RenderElement<AiModel>(nameof(AiModel.Endpoint)),
                new RenderElement<AiModel>(nameof(AiModel.Deployment)),
                new RenderElement<AiModel>(nameof(AiModel.OpenAIModel)),
                new RenderElement<AiModel>(nameof(AiModel.TrustLevel)),
                new RenderElement<AiModel>(nameof(AiModel.SystemPrompt)),
            });
    }

    internal static string ExportModel(this Configuration config, string name, FileInfo file, bool ignoreApiKey)
    {
        IList<AiModel> models;
        if (name is null)
        {
            models = config.Models;
        }
        else
        {
            AiModel model = config.GetModelByName(name)
                ?? throw new ArgumentException($"A model with the name <{name}> cannot be found.", nameof(name));
            models = new[] { model };
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            TypeInfoResolver = new AIModelContractResolver(ignoreApiKey)
        };

        if (file is null)
        {
            return JsonSerializer.Serialize(models, options);
        }

        using var stream = file.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, models, options);

        return null;
    }
}
