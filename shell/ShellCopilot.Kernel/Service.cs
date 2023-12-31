using Azure;
using Azure.Core;
using Azure.AI.OpenAI;
using SharpToken;

namespace ShellCopilot.Kernel;

internal class ChatResponse
{
    internal ChatResponse(ChatChoice choice)
    {
        Content = choice.Message.Content;
        FinishReason = choice.FinishReason;
    }

    internal string Content { get; }
    internal CompletionsFinishReason FinishReason { get; }
}

internal class BackendService
{
    // TODO: Maybe expose this to our model registration?
    // We can still use 1000 as the default value.
    private const int MaxResponseToken = 1000;

    private OpenAIClient _client;
    private AiModel _activeModel;

    private readonly string _historyFileNamePrefix;
    private readonly List<ChatMessage> _chatHistory;
    private readonly Configuration _config;

    internal BackendService(Configuration config, string historyFileNamePrefix)
    {
        _config = config;
        _chatHistory = new List<ChatMessage>();
        _historyFileNamePrefix = historyFileNamePrefix ?? $"history.{Utils.GetParentProcessId()}";
    }

    internal List<ChatMessage> ChatHistory => _chatHistory;

    // TODO: chat history loading/saving
    private void LoadChatHistory()
    {

    }

    private void SaveChatHistory()
    {
        if (_chatHistory.Count is 0)
        {
            return;
        }
    }

    private void RefreshOpenAIClient()
    {
        AiModel modelInUse = _config.GetModelInUse();
        if (_activeModel == modelInUse)
        {
            // Active model was not changed.
            return;
        }

        AiModel old = _activeModel;
        _activeModel = modelInUse;

        if (old is not null
            && string.Equals(old.Endpoint, _activeModel.Endpoint)
            && old.Key.Length == _activeModel.Key.Length)
        {
            // It's the same same endpoint, so we reuse the existing client.
            return;
        }

        var clientOptions = new OpenAIClientOptions() { RetryPolicy = new ChatRetryPolicy() };
        bool isApimEndpoint = _activeModel.Endpoint.EndsWith(Utils.ApimGatewayDomain);

        if (isApimEndpoint)
        {
            string userkey = Utils.ConvertFromSecureString(_activeModel.Key);
            clientOptions.AddPolicy(
                new UserKeyPolicy(
                    new AzureKeyCredential(userkey),
                    Utils.ApimAuthorizationHeader),
                HttpPipelinePosition.PerRetry
            );
        }

        string azOpenAIApiKey = isApimEndpoint
            ? "placeholder-api-key"
            : Utils.ConvertFromSecureString(_activeModel.Key);

        _client = new(
            new Uri(_activeModel.Endpoint),
            new AzureKeyCredential(azOpenAIApiKey),
            clientOptions);
    }

    private int CountTokenForMessages(IEnumerable<ChatMessage> messages)
    {
        ModelDetail modelDetail = _activeModel.ModelDetail;
        GptEncoding encoding = modelDetail.GptEncoding;
        int tokensPerMessage = modelDetail.TokensPerMessage;
        int tokensPerName = modelDetail.TokensPerName;

        int tokenNumber = 0;
        foreach (ChatMessage message in messages)
        {
            tokenNumber += tokensPerMessage;
            tokenNumber += encoding.Encode(message.Role.ToString()).Count;
            tokenNumber += encoding.Encode(message.Content).Count;

            if (message.Name is not null)
            {
                tokenNumber += tokensPerName;
                tokenNumber += encoding.Encode(message.Name).Count;
            }
        }

        // Every reply is primed with <|start|>assistant<|message|>, which takes 3 tokens.
        tokenNumber += 3;
        return tokenNumber;
    }

    private void ReduceChatHistoryAsNeeded(List<ChatMessage> history, ChatMessage input)
    {
        int totalTokens = CountTokenForMessages(Enumerable.Repeat(input, 1));
        int tokenLimit = _activeModel.ModelDetail.TokenLimit;

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
        // Refresh the client in case the active model was changed.
        RefreshOpenAIClient();

        // TODO: Shall we expose some of the setting properties to our model registration?
        //  - max_tokens
        //  - temperature
        //  - top_p
        //  - presence_penalty
        //  - frequency_penalty
        // Those settings seem to be important enough, as the Semantic Kernel plugin specifies
        // those settings (see the URL below). We can use default values when not defined.
        // https://github.com/microsoft/semantic-kernel/blob/main/samples/skills/FunSkill/Joke/config.json
        ChatCompletionsOptions chatOptions = new()
        {
            ChoiceCount = 1,
            Temperature = (float)0.7,
            MaxTokens = MaxResponseToken,
        };

        List<ChatMessage> history = insertToHistory ? _chatHistory : new List<ChatMessage>();
        if (history.Count is 0)
        {
            history.Add(new ChatMessage(ChatRole.System, _activeModel.SystemPrompt));
        }

        ReduceChatHistoryAsNeeded(history, new ChatMessage(ChatRole.User, input));
        foreach (ChatMessage message in history)
        {
            chatOptions.Messages.Add(message);
        }

        return chatOptions;
    }

    public async Task<ChatResponse> GetChatResponseAsync(string input, bool insertToHistory = true, CancellationToken cancellationToken = default)
    {
        try
        {
            ChatCompletionsOptions chatOptions = PrepareForChatCompletion(input, insertToHistory);
            Response<ChatCompletions> response = await _client.GetChatCompletionsAsync(_activeModel.Deployment, chatOptions, cancellationToken);
            return new ChatResponse(response.Value.Choices[0]);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<StreamingChatCompletions> GetStreamingChatResponseAsync(string input, bool insertToHistory = true, CancellationToken cancellationToken = default)
    {
        try
        {
            ChatCompletionsOptions chatOptions = PrepareForChatCompletion(input, insertToHistory);
            Response<StreamingChatCompletions> response = await _client.GetChatCompletionsStreamingAsync(_activeModel.Deployment, chatOptions, cancellationToken);
            return response.Value;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
