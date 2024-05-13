using SharpToken;

internal class ModelInfo
{
    private static readonly ModelInfo GPT4 = new(tokenLimit: 8_192, tokensPerMessage: 3, tokensPerName: 1);
    private static readonly ModelInfo GPT4_32K = new(tokenLimit: 32_768, tokensPerMessage: 3, tokensPerName: 1);
    private static readonly ModelInfo GPT35_0301 = new(tokenLimit: 4_096, tokensPerMessage: 4, tokensPerName: -1);
    private static readonly ModelInfo GPT35_0613 = new(tokenLimit: 4_096, tokensPerMessage: 3, tokensPerName: 1);
    private static readonly ModelInfo GPT35_16K = new(tokenLimit: 16_385, tokensPerMessage: 3, tokensPerName: 1);

    // For reference, see https://platform.openai.com/docs/models and the "Counting tokens" section in
    // https://github.com/openai/openai-cookbook/blob/main/examples/How_to_format_inputs_to_ChatGPT_models.ipynb
    private static readonly Dictionary<string, ModelInfo> s_modelMap = new()
    {
        ["gpt-4"]      = GPT4,
        ["gpt-4-0314"] = GPT4,
        ["gpt-4-0613"] = GPT4,

        ["gpt-4-32k"]      = GPT4_32K,
        ["gpt-4-32k-0314"] = GPT4_32K,
        ["gpt-4-32k-0613"] = GPT4_32K,

        ["gpt-3.5-turbo"]      = GPT35_0613,
        ["gpt-3.5-turbo-0301"] = GPT35_0301,
        ["gpt-3.5-turbo-0613"] = GPT35_0613,

        ["gpt-3.5-turbo-16k"]      = GPT35_16K,
        ["gpt-3.5-turbo-16k-0613"] = GPT35_16K,

        // Azure naming of the 'gpt-3.5-turbo' models
        ["gpt-35-turbo-0301"]     = GPT35_0301,
        ["gpt-35-turbo-0613"]     = GPT35_0613,
        ["gpt-35-turbo-16k-0613"] = GPT35_16K,
    };

    private ModelInfo(int tokenLimit, int tokensPerMessage, int tokensPerName)
    {
        TokenLimit = tokenLimit;
        TokensPerMessage = tokensPerMessage;
        TokensPerName = tokensPerName;
    }

    internal int TokenLimit { get; }
    internal int TokensPerMessage { get; }
    internal int TokensPerName { get; }

    private GptEncoding _gptEncoding = null;

    /// <summary>
    /// Models gpt4, gpt3.5, and the variants of them are all using the 'cl100k_base' token encoding. For reference:
    ///  https://github.com/openai/tiktoken/blob/5d970c1100d3210b42497203d6b5c1e30cfda6cb/tiktoken/model.py#L7
    ///  https://github.com/dmitry-brazhenko/SharpToken/blob/main/SharpToken/Lib/Model.cs#L8
    /// </summary>
    internal GptEncoding GptEncoding => _gptEncoding ??= GptEncoding.GetEncoding("cl100k_base");

    /// <summary>
    /// Try resolving the specified model name.
    /// </summary>
    internal static bool TryResolve(string name, out ModelInfo model)
    {
        return s_modelMap.TryGetValue(name, out model);
    }

    internal static ModelInfo GetByName(string name)
    {
        return s_modelMap[name] ?? throw new ArgumentException($"Invalid key '{name}'", nameof(name));
    }

    internal static IEnumerable<string> SupportedModels()
    {
        return s_modelMap.Keys.SkipWhile(n => n.StartsWith("gpt-35")).OrderDescending();
    }
}
