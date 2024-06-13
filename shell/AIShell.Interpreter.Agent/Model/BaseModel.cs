using Azure.AI.OpenAI;
using Azure;
using AIShell.Abstraction;

namespace AIShell.Interpreter.Agent;

/// <summary>
/// The base model class for LLMs. Implementations are FunctionCallingModel and TextBasedModels.
/// </summary>
internal abstract class BaseModel
{
    internal ChatService ChatService;
    internal IHost Host;
    internal CodeExecutionService ExecutionService;
    internal bool AutoExecution;
    internal bool DisplayErrors;

    /// <summary>
    /// Extracts code from the response and calls the appropriate method to execute the code.
    /// </summary>
    /// <param name="responseContent"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected abstract Task<InternalChatResultsPacket> HandleFunctionCall(string responseContent, CancellationToken token);

    protected BaseModel(
        bool autoExecution, 
        bool displayErrors, 
        ChatService chatService, 
        CodeExecutionService executionService,
        IHost host)
    {
        ChatService = chatService;
        Host = host;
        ExecutionService = executionService;
        AutoExecution = autoExecution;
        DisplayErrors = displayErrors;
    }

    public async Task<InternalChatResultsPacket> SmartChat(string input, RenderingStyle renderingStyle, CancellationToken token)
    {
        string responseContent = null;
        if (renderingStyle is RenderingStyle.FullResponsePreferred)
        {
            // TODO: Add a way to handle the response if it is a tool call
            // TODO: Test FullResponsePreferred
            ChatRequestUserMessage chatRequestUserMessage = new(input);
            Task<Response<ChatCompletions>> func_non_streaming() => ChatService.GetChatCompletionsAsync(chatRequestUserMessage, token);
            Response<ChatCompletions> response = await Host.RunWithSpinnerAsync(func_non_streaming).ConfigureAwait(false);

            if (response is not null)
            {
                ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
                Host.RenderFullResponse(responseContent);

                ChatChoice responseChoice = response.Value.Choices[0];
                if (responseChoice.FinishReason is CompletionsFinishReason FinishReason)
                {
                    //TODO need to rework. This is a temporary fix
                    string warning = "";
                    if (warning is not null)
                    {
                        Host.MarkupWarningLine(warning);
                        Host.WriteLine();
                    }
                }
                // ChatService.AddToolCallToHistory(response);
            }
        }
        else
        {
            ChatRequestUserMessage chatRequestUserMessage = new(input);
            Task<StreamingResponse<StreamingChatCompletionsUpdate>> func_streaming() => ChatService.GetStreamingChatResponseAsync(chatRequestUserMessage, token);
            StreamingResponse<StreamingChatCompletionsUpdate> response = await Host.RunWithSpinnerAsync(func_streaming).ConfigureAwait(false);
            if (response is not null)
            {
                using var streamingRender = Host.NewStreamRender(token);
                try
                {
                    await foreach (StreamingChatCompletionsUpdate chatUpdate in response)
                    {
                        RenderStreamingChat(streamingRender, chatUpdate);
                    }
                    responseContent = streamingRender.AccumulatedContent;
                }
                catch (OperationCanceledException)
                {
                    return new InternalChatResultsPacket("AI response cancelled.", "Tool was not called.");
                }
            }
            else
            {
                return new InternalChatResultsPacket("AI response cancelled.","Tool was not called.");
            }
        }

        return await HandleFunctionCall(responseContent, token);
    }

    /// <summary>
    /// Renders the streaming chat completions.
    /// </summary>
    /// <param name="streamingRender"></param>
    /// <param name="chatUpdate"></param>
    protected virtual void RenderStreamingChat(IStreamRender streamingRender, StreamingChatCompletionsUpdate chatUpdate)
    {
        if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
        {
            streamingRender.Refresh(chatUpdate.ContentUpdate);
        }
    }
}

