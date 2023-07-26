using Azure;
using Azure.Core;
using Azure.AI.OpenAI;
using SharpToken;

namespace Shell;

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

    // TODO: Maybe expose this to our model registration?
    // We can still use 250 as the default value.
    private const int MaxResponseToken = 250;

    private OpenAIClient _client;
    private AIModel _activeModel;
    private string _chatHistoryFile;
    private List<ChatMessage> _chatHistory;
    private readonly ServiceConfig _config;

    internal BackendService(bool loadChatHistory)
    {
        _config = ServiceConfig.ReadFromConfigFile();
        _chatHistory = new List<ChatMessage>();
        _chatHistoryFile = Path.Combine(Utils.AppConfigHome, $"{Utils.AppName}.history.{Utils.GetParentProcessId()}");

        _activeModel = _config.GetModelInUse();
        _client = NewOpenAIClient(_activeModel);
    }

    internal ServiceConfig Configuration => _config;

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
