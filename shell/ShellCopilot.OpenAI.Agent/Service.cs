using System.Diagnostics;
using Azure;
using Azure.Core;
using Azure.AI.OpenAI;
using SharpToken;

namespace AIShell.OpenAI.Agent;

internal class ChatService
{
    // TODO: Maybe expose this to our model registration?
    // We can still use 1000 as the default value.
    private const int MaxResponseToken = 1000;
    private readonly string _historyRoot;
    private readonly List<ChatRequestMessage> _chatHistory;

    private GPT _gptToUse;
    private Settings _settings;
    private OpenAIClient _client;

    internal ChatService(string historyRoot, Settings settings)
    {
        _historyRoot = historyRoot;
        _settings = settings;
        _chatHistory = [];
    }

    internal List<ChatRequestMessage> ChatHistory => _chatHistory;

    internal void AddResponseToHistory(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return;
        }

        _chatHistory.Add(new ChatRequestAssistantMessage(response));
    }

    internal void RefreshSettings(Settings settings)
    {
        _settings = settings;
    }

    private void RefreshOpenAIClient()
    {
        if (ReferenceEquals(_gptToUse, _settings.Active))
        {
            // Active GPT was not changed.
            return;
        }

        GPT old = _gptToUse;
        _gptToUse = _settings.Active;
        _chatHistory.Clear();

        if (old is not null
            && old.Type == _gptToUse.Type
            && string.Equals(old.Endpoint, _gptToUse.Endpoint)
            && string.Equals(old.Deployment, _gptToUse.Deployment)
            && string.Equals(old.ModelName, _gptToUse.ModelName)
            && old.Key.IsEqualTo(_gptToUse.Key))
        {
            // It's the same same endpoint, so we reuse the existing client.
            return;
        }

        var clientOptions = new OpenAIClientOptions() { RetryPolicy = new ChatRetryPolicy() };

        if (_gptToUse.Type is EndpointType.AzureOpenAI)
        {
            // Create a client that targets Azure OpenAI service or Azure API Management service.
            bool isApimEndpoint = _gptToUse.Endpoint.EndsWith(Utils.ApimGatewayDomain);
            if (isApimEndpoint)
            {
                string userkey = Utils.ConvertFromSecureString(_gptToUse.Key);
                clientOptions.AddPolicy(
                    new UserKeyPolicy(
                        new AzureKeyCredential(userkey),
                        Utils.ApimAuthorizationHeader),
                    HttpPipelinePosition.PerRetry
                );
            }

            string azOpenAIApiKey = isApimEndpoint
                ? "placeholder-api-key"
                : Utils.ConvertFromSecureString(_gptToUse.Key);

            _client = new OpenAIClient(
                new Uri(_gptToUse.Endpoint),
                new AzureKeyCredential(azOpenAIApiKey),
                clientOptions);
        }
        else
        {
            // Create a client that targets the non-Azure OpenAI service.
            _client = new OpenAIClient(Utils.ConvertFromSecureString(_gptToUse.Key), clientOptions);
        }
    }

    private int CountTokenForMessages(IEnumerable<ChatRequestMessage> messages)
    {
        ModelInfo modelDetail = _gptToUse.ModelInfo;
        GptEncoding encoding = modelDetail.Encoding;
        int tokensPerMessage = modelDetail.TokensPerMessage;
        int tokensPerName = modelDetail.TokensPerName;

        int tokenNumber = 0;
        foreach (ChatRequestMessage message in messages)
        {
            tokenNumber += tokensPerMessage;
            tokenNumber += encoding.Encode(message.Role.ToString()).Count;

            switch (message)
            {
                case ChatRequestSystemMessage systemMessage:
                    tokenNumber += SimpleCountToken(systemMessage.Content, systemMessage.Name);
                    break;

                case ChatRequestUserMessage userMessage:
                    tokenNumber += SimpleCountToken(userMessage.Content, userMessage.Name);
                    break;

                case ChatRequestAssistantMessage assistantMessage:
                    tokenNumber += SimpleCountToken(assistantMessage.Content, assistantMessage.Name);
                    if (assistantMessage.ToolCalls is not null)
                    {
                        // Count tokens for the tool call's properties
                        foreach(ChatCompletionsToolCall chatCompletionsToolCall in assistantMessage.ToolCalls)
                        {
                            if(chatCompletionsToolCall is ChatCompletionsFunctionToolCall functionToolCall)
                            {
                                tokenNumber += encoding.Encode(functionToolCall.Id).Count;
                                tokenNumber += encoding.Encode(functionToolCall.Name).Count;
                                tokenNumber += encoding.Encode(functionToolCall.Arguments).Count;
                            }
                        }
                    }
                    break;

                case ChatRequestToolMessage toolMessage:
                    tokenNumber += encoding.Encode(toolMessage.ToolCallId).Count;
                    tokenNumber += encoding.Encode(toolMessage.Content).Count;
                    break;
                    // Add cases for other derived types as needed
            }
        }

        // Every reply is primed with <|start|>assistant<|message|>, which takes 3 tokens.
        tokenNumber += 3;
        return tokenNumber;

        // ----- Local Function -----
        int SimpleCountToken(string content, string name)
        {
            int sum = 0;
            if (!string.IsNullOrEmpty(content))
            {
                sum = encoding.Encode(content).Count;
            }

            if (!string.IsNullOrEmpty(name))
            {
                sum += tokensPerName;
                sum += encoding.Encode(name).Count;
            }

            return sum;
        }
    }

    private void ReduceChatHistoryAsNeeded(List<ChatRequestMessage> history, ChatRequestMessage input)
    {
        bool inputTooLong = false;
        int tokenLimit = _gptToUse.ModelInfo.TokenLimit;

        do
        {
            int totalTokens = CountTokenForMessages(Enumerable.Repeat(input, 1));
            if (totalTokens + MaxResponseToken >= tokenLimit)
            {
                // The input itself already exceeds the token limit.
                inputTooLong = true;
                break;
            }

            history.Add(input);
            totalTokens = CountTokenForMessages(history);

            int index = -1;
            while (totalTokens + MaxResponseToken >= tokenLimit)
            {
                if (index is -1)
                {
                    // Find the first non-system message.
                    for (index = 0; history[index] is ChatRequestSystemMessage; index++);
                }

                if (history[index] == input)
                {
                    // The input plus system message exceeds the token limit.
                    inputTooLong = true;
                    break;
                }

                history.RemoveAt(index);
                totalTokens = CountTokenForMessages(history);
            }
        }
        while (false);

        if (inputTooLong)
        {
            var message = $"The input is too long to get a proper response without exceeding the token limit ({tokenLimit}).\nPlease reduce the input and try again.";
            throw new InvalidOperationException(message);
        }
    }

    private ChatCompletionsOptions PrepareForChat(string input)
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
        string deploymentOrModelName = _gptToUse.Type switch
        {
            EndpointType.AzureOpenAI => _gptToUse.Deployment,
            EndpointType.OpenAI => _gptToUse.ModelName,
            _ => throw new UnreachableException(),
        };

        ChatCompletionsOptions chatOptions = new()
        {
            DeploymentName = deploymentOrModelName,
            ChoiceCount = 1,
            Temperature = 0,
            MaxTokens = MaxResponseToken,
        };

        List<ChatRequestMessage> history = _chatHistory;
        if (history.Count is 0)
        {
            history.Add(new ChatRequestSystemMessage(_gptToUse.SystemPrompt));
        }

        ReduceChatHistoryAsNeeded(history, new ChatRequestUserMessage(input));
        foreach (ChatRequestMessage message in history)
        {
            chatOptions.Messages.Add(message);
        }

        return chatOptions;
    }

    public async Task<StreamingResponse<StreamingChatCompletionsUpdate>> GetStreamingChatResponseAsync(string input, CancellationToken cancellationToken = default)
    {
        try
        {
            ChatCompletionsOptions chatOptions = PrepareForChat(input);
            var response = await _client.GetChatCompletionsStreamingAsync(
                chatOptions,
                cancellationToken);

            return response;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
