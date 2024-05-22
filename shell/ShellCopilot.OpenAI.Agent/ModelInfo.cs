using SharpToken;

internal class ModelInfo
{
    private static readonly ModelInfo gpt4 = new(tokenLimit: 8_192);
    private static readonly ModelInfo gpt4_32k = new(tokenLimit: 32_768);
    private static readonly ModelInfo gpt4_turbo = new(tokenLimit: 128_000);
    private static readonly ModelInfo gpt4o = new(tokenLimit: 128_000, encoding: "o200k_base");
    private static readonly ModelInfo gpt35_turbo = new(tokenLimit: 16_385);

    // For reference, see https://platform.openai.com/docs/models and the "Counting tokens" section in
    // https://github.com/openai/openai-cookbook/blob/main/examples/How_to_format_inputs_to_ChatGPT_models.ipynb
    // https://github.com/openai/openai-cookbook/blob/main/examples/How_to_count_tokens_with_tiktoken.ipynb
    private static readonly Dictionary<string, ModelInfo> s_modelMap = new()
    {
        ["gpt-4o"]        = gpt4o,
        ["gpt-4"]         = gpt4,
        ["gpt-4-32k"]     = gpt4_32k,
        ["gpt-4-turbo"]   = gpt4_turbo,
        ["gpt-3.5-turbo"] = gpt35_turbo,
        // Azure naming of the 'gpt-3.5-turbo' models
        ["gpt-35-turbo"]  = gpt35_turbo,
    };

    private ModelInfo(int tokenLimit, string encoding = null)
    {
        TokenLimit = tokenLimit;

        // Models gpt4, gpt3.5, and the variants of them are all using the 'cl100k_base' token encoding,
        // so we use this encoding by default. For reference:
        // https://github.com/openai/tiktoken/blob/5d970c1100d3210b42497203d6b5c1e30cfda6cb/tiktoken/model.py#L7
        // https://github.com/dmitry-brazhenko/SharpToken/blob/main/SharpToken/Lib/Model.cs#L8
        encoding ??= "cl100k_base";
        Encoding = GptEncoding.GetEncoding(encoding);

        // For gpt4 and gpt3.5-turbo, the following 2 properties are the same.
        // See https://github.com/openai/openai-cookbook/blob/main/examples/How_to_count_tokens_with_tiktoken.ipynb
        TokensPerMessage = 3;
        TokensPerName = 1;
    }

    internal int TokenLimit { get; }
    internal int TokensPerMessage { get; }
    internal int TokensPerName { get; }
    internal GptEncoding Encoding { get; }

    /// <summary>
    /// Try resolving the specified model name.
    /// </summary>
    internal static bool TryResolve(string name, out ModelInfo model)
    {
        if (s_modelMap.TryGetValue(name, out model))
        {
            return true;
        }

        int lastDashIndex = name.LastIndexOf('-');
        while (lastDashIndex > 0)
        {
            string parentName = name[..lastDashIndex];
            if (s_modelMap.TryGetValue(parentName, out model))
            {
                return true;
            }

            lastDashIndex = parentName.LastIndexOf('-');
        }

        return false;
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
