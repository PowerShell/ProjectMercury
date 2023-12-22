using System.Diagnostics;
using System.Reflection;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using SharpToken;
using Spectre.Console;

namespace ShellCopilot.Kernel;

public enum TrustLevel
{
    Private,
    Public,
}

internal class ModelDetail
{
    private ModelDetail(int tokenLimit, int tokensPerMessage, int tokensPerName)
    {
        TokenLimit = tokenLimit;
        TokensPerMessage = tokensPerMessage;
        TokensPerName = tokensPerName;
    }

    private GptEncoding _gptEncoding = null;

    internal static ModelDetail GPT4 = new(tokenLimit: 8_192, tokensPerMessage: 3, tokensPerName: 1);
    internal static ModelDetail GPT4_32K = new(tokenLimit: 32_768, tokensPerMessage: 3, tokensPerName: 1);
    internal static ModelDetail GPT35_0301 = new(tokenLimit: 4_096, tokensPerMessage: 4, tokensPerName: -1);
    internal static ModelDetail GPT35_0613 = new(tokenLimit: 4_096, tokensPerMessage: 3, tokensPerName: 1);
    internal static ModelDetail GPT35_16K = new(tokenLimit: 16_385, tokensPerMessage: 3, tokensPerName: 1);

    internal int TokenLimit { get; }
    internal int TokensPerMessage { get; }
    internal int TokensPerName { get; }

    // Models gpt4, gpt3.5, and the variants of them are all using the 'cl100k_base' token encoding.
    // For reference:
    //   https://github.com/openai/tiktoken/blob/5d970c1100d3210b42497203d6b5c1e30cfda6cb/tiktoken/model.py#L7
    //   https://github.com/dmitry-brazhenko/SharpToken/blob/main/SharpToken/Lib/Model.cs#L8
    internal GptEncoding GptEncoding => _gptEncoding ??= GptEncoding.GetEncoding("cl100k_base");
}

public class AiModel
{
    // For reference, see https://platform.openai.com/docs/models and the "Counting tokens" section in
    // https://github.com/openai/openai-cookbook/blob/main/examples/How_to_format_inputs_to_ChatGPT_models.ipynb
    private static readonly Dictionary<string, ModelDetail> ModelToTokenLimitMapping = new()
    {
        ["gpt-4"]      = ModelDetail.GPT4,
        ["gpt-4-0314"] = ModelDetail.GPT4,
        ["gpt-4-0613"] = ModelDetail.GPT4,

        ["gpt-4-32k"]      = ModelDetail.GPT4_32K,
        ["gpt-4-32k-0314"] = ModelDetail.GPT4_32K,
        ["gpt-4-32k-0613"] = ModelDetail.GPT4_32K,

        ["gpt-3.5-turbo"]      = ModelDetail.GPT35_0613,
        ["gpt-3.5-turbo-0301"] = ModelDetail.GPT35_0301,
        ["gpt-3.5-turbo-0613"] = ModelDetail.GPT35_0613,

        ["gpt-3.5-turbo-16k"]      = ModelDetail.GPT35_16K,
        ["gpt-3.5-turbo-16k-0613"] = ModelDetail.GPT35_16K,

        // Azure naming of the 'gpt-3.5-turbo' models
        ["gpt-35-turbo-0301"]     = ModelDetail.GPT35_0301,
        ["gpt-35-turbo-0613"]     = ModelDetail.GPT35_0613,
        ["gpt-35-turbo-16k-0613"] = ModelDetail.GPT35_16K,
    };

    private string _prompt;
    private string _endpoint;
    private string _deployment;
    private string _openAIModel;
    private ModelDetail _modelDetail;

    public AiModel(
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
        _endpoint = endpoint.Trim().TrimEnd('/');
        _deployment = deployment;

        SetOpenAIModel(openAIModel);

        Description = description;
        Key = key;
        TrustLevel = trustLevel;
    }

    private void SetOpenAIModel(string openAIModel)
    {
        _openAIModel = openAIModel.ToLowerInvariant();

        if (!ModelToTokenLimitMapping.TryGetValue(_openAIModel, out _modelDetail))
        {
            var message = $"The specified '{_openAIModel}' is not a supported OpenAI chat completion model.";
            throw new ArgumentException(message, nameof(openAIModel));
        }

        // For Azure OpenAI, we require the version to be specified in the model name.
        // TODO: When using OpenAI, it's OK for the model name to not include the version number.
        //       So, the logic here needs to be updated when support OpenAI.
        int lastDashIndex = _openAIModel.LastIndexOf('-');
        ReadOnlySpan<char> span = _openAIModel.AsSpan(lastDashIndex + 1);
        if (span.Length is not 4 || !int.TryParse(span, out _))
        {
            var message = $"For Azure OpenAI endpoint, please also specify the model version in the name. For example: 'gpt-4-0613'.";
            throw new ArgumentException(message, nameof(openAIModel));
        }
    }

    internal ModelDetail ModelDetail => _modelDetail;

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
            SetOpenAIModel(value);
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

    internal bool RequestForKey(bool mandatory, CancellationToken cancellationToken, bool showBackendInfo = true)
    {
        return RequestForKeyAsync(mandatory, cancellationToken, showBackendInfo).GetAwaiter().GetResult();
    }

    internal async Task<bool> RequestForKeyAsync(bool mandatory, CancellationToken cancellationToken, bool showBackendInfo = true)
    {
        Debug.Assert(Key is null, "Expect the key to be missing.");

        bool askForKey;
        string message = ConsoleRender.FormatNote($"The access key is missing for the model [green]'{Name}'[/]");

        if (showBackendInfo)
        {
            AnsiConsole.MarkupLine($"{message}:");
            DisplayBackendInfo();
        }
        else
        {
            AnsiConsole.MarkupLine($"{message}.");
            AnsiConsole.WriteLine();
        }

        if (Utils.ShellCopilotEndpoint.Equals(_endpoint, StringComparison.OrdinalIgnoreCase))
        {
            string docLink = AnsiConsole.Profile.Capabilities.Links
                ? $"[green][link={Utils.KeyApplicationHelpLink}]the instructions[/][/] in our doc"
                : $"the instructions at {Utils.KeyApplicationHelpLink}";

            AnsiConsole.MarkupLine("[grey]> The model uses the default ShellCopilot endpoint.[/]");
            AnsiConsole.MarkupLine($"[grey]> You can apply an access key for it by following {docLink}.[/]");
            AnsiConsole.WriteLine();
        }

        if (mandatory)
        {
            askForKey = true;
            AnsiConsole.MarkupLine("[bold]Please enter the access key to continue ...[/]");
        }
        else
        {
            try
            {
                askForKey = await ConsoleRender
                    .AskForConfirmation("[bold]  Enter the access key now?[/]", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // User cancelled the prompt and we consider it as declining.
                askForKey = false;
            }
        }

        if (askForKey)
        {
            try
            {
                string secret = await ConsoleRender.AskForSecret("Enter [green]Key[/]:", cancellationToken).ConfigureAwait(false);
                Key = Utils.ConvertDataToSecureString(secret);

                AnsiConsole.WriteLine();
                return true;
            }
            catch (OperationCanceledException)
            {
                // User cancelled the prompt.
                AnsiConsole.MarkupLine("\n\n[bold]Operation cancelled.[/]");
                return false;
            }
        }

        // User declined, so model is not changed.
        return false;
    }

    internal void DisplayBackendInfo()
    {
        ConsoleRender.RenderList(
            this,
            new[]
            {
                new RenderElement<AiModel>(label: "  Endpoint", m => m.Endpoint),
                new RenderElement<AiModel>(label: "  Deployment", m => m.Deployment),
            });
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

        if (_ignoreKey && typeInfo.Type == typeof(AiModel))
        {
            int index = 0;
            for (; index < typeInfo.Properties.Count; index++)
            {
                if (typeInfo.Properties[index].Name is nameof(AiModel.Key))
                {
                    break;
                }
            }

            typeInfo.Properties.RemoveAt(index);
        }

        return typeInfo;
    }
}
