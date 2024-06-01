using SharpToken;

namespace ShellCopilot.OpenAI.Agent;

internal class ModelInfo
{
    // Models gpt4, gpt3.5, and the variants of them all use the 'cl100k_base' token encoding.
    // But the gpt-4o model uses the 'o200k_base' token encoding. For reference:
    //   https://github.com/openai/tiktoken/blob/5d970c1100d3210b42497203d6b5c1e30cfda6cb/tiktoken/model.py#L7
    //   https://github.com/dmitry-brazhenko/SharpToken/blob/main/SharpToken/Lib/Model.cs#L8
    private const string Gpt4oEncoding = "o200k_base";
    private const string Gpt34Encoding = "cl100k_base";

    private static readonly Dictionary<string, ModelInfo> s_modelMap;
    private static readonly Dictionary<string, Task<GptEncoding>> s_encodingMap;

    static ModelInfo()
    {
        // For reference, see https://platform.openai.com/docs/models and the "Counting tokens" section in
        // https://github.com/openai/openai-cookbook/blob/main/examples/How_to_format_inputs_to_ChatGPT_models.ipynb
        // https://github.com/openai/openai-cookbook/blob/main/examples/How_to_count_tokens_with_tiktoken.ipynb
        s_modelMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o"]        = new(tokenLimit: 128_000, encoding: Gpt4oEncoding),
            ["gpt-4"]         = new(tokenLimit: 8_192),
            ["gpt-4-32k"]     = new(tokenLimit: 32_768),
            ["gpt-4-turbo"]   = new(tokenLimit: 128_000),
            ["gpt-3.5-turbo"] = new(tokenLimit: 16_385),
            // Azure naming of the 'gpt-3.5-turbo' models
            ["gpt-35-turbo"]  = new(tokenLimit: 16_385),
        };

        // The first call to 'GptEncoding.GetEncoding' is very slow, taking about 2 seconds on my machine.
        // We don't immediately need the encodings at the startup, so by getting the values in tasks,
        // we don't block the startup and the values will be ready when we really need them.
        s_encodingMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [Gpt34Encoding] = Task.Run(() => GptEncoding.GetEncoding(Gpt34Encoding)),
            [Gpt4oEncoding] = Task.Run(() => GptEncoding.GetEncoding(Gpt4oEncoding))
        };
    }

    private ModelInfo(int tokenLimit, string encoding = null)
    {
        TokenLimit = tokenLimit;
        _encodingName = encoding ?? Gpt34Encoding;

        // For gpt4 and gpt3.5-turbo, the following 2 properties are the same.
        // See https://github.com/openai/openai-cookbook/blob/main/examples/How_to_count_tokens_with_tiktoken.ipynb
        TokensPerMessage = 3;
        TokensPerName = 1;
    }

    private readonly string _encodingName;
    private GptEncoding _gptEncoding;

    internal int TokenLimit { get; }
    internal int TokensPerMessage { get; }
    internal int TokensPerName { get; }
    internal GptEncoding Encoding
    {
        get {
            _gptEncoding ??= s_encodingMap.TryGetValue(_encodingName, out Task<GptEncoding> value)
                ? value.Result
                : GptEncoding.GetEncoding(_encodingName);
            return _gptEncoding;
        }
    }

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
