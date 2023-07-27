using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ShellCopilot;

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

    private string _prompt;
    private string _endpoint;
    private string _deployment;
    private string _openAIModel;
    private string _simpleOpenAIModelName;
    private int _tokenLimit;

    public AIModel(
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

        Name = name;
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

    internal string SimpleOpenAIModelName => _simpleOpenAIModelName;
    internal int TokenLimit => _tokenLimit;

    public string Name { get; private set; }

    public string Description { get; internal set; } 

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

    [JsonConverter(typeof(SecureStringJsonConverter))]
    public SecureString Key { get; internal set; }

    public TrustLevel TrustLevel { get; internal set; }

    public string SystemPrompt
    {
        get { return _prompt; }

        internal set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            _prompt = value;
        }
    }
}

internal class SecureStringJsonConverter : JsonConverter<SecureString>
{
    public override SecureString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string payload = reader.GetString();
        return Utils.ConvertDataToSecureString(payload);
    }

    public override void Write(Utf8JsonWriter writer, SecureString value, JsonSerializerOptions options)
    {
        string payload = Utils.GetDataFromSecureString(value);
        writer.WriteStringValue(payload);
    }
}

internal class AIModelContractResolver : DefaultJsonTypeInfoResolver
{
    private readonly bool _ignoreKey;
    internal AIModelContractResolver(bool ignoreKey)
    {
        _ignoreKey = ignoreKey;
    }

    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo typeInfo = base.GetTypeInfo(type, options);

        if (_ignoreKey && typeInfo.Type == typeof(AIModel))
        {
            int index = 0;
            for (; index < typeInfo.Properties.Count; index++)
            {
                if (typeInfo.Properties[index].Name is nameof(AIModel.Key))
                {
                    break;
                }
            }

            typeInfo.Properties.RemoveAt(index);
        }

        return typeInfo;
    }
}
