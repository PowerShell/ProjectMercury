using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Interpreter.Agent;

public abstract class BaseModel : IModel
{
    internal ChatService ChatService;
    internal IHost Host;
    internal Computer computer;
    internal bool AutoExecution;
    internal bool DisplayErrors;

    protected abstract Task<InternalChatResultsPacket> HandleFunctionCall(string responseContent, CancellationToken token);

    internal BaseModel(
        bool autoExecution, 
        bool displayErrors, 
        ChatService chatService, 
        IHost host)
    {
        ChatService = chatService;
        this.Host = host;
        computer = new Computer();
        AutoExecution = autoExecution;
        DisplayErrors = displayErrors;
    }

    public async Task<InternalChatResultsPacket> SmartChat(string input, RenderingStyle _renderingStyle, CancellationToken token)
    {
        string responseContent = null;
        if (_renderingStyle is RenderingStyle.FullResponsePreferred)
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
                    // Cannot pass in `cancellationToken` to `GetChoicesStreaming()` and `GetMessageStreaming()` methods.
                    // Doing so will result in an exception in Azure.Open
                    await foreach (StreamingChatCompletionsUpdate chatUpdate in response)
                    {
                        RenderStreamingChat(streamingRender, chatUpdate);
                    }
                    responseContent = streamingRender.AccumulatedContent;
                }
                catch (OperationCanceledException)
                {
                    // Ignore the cancellation exception.
                }
            }
            else
            {
                return new InternalChatResultsPacket("AI response cancelled.","Tool was not called.");
            }
        }

        return await HandleFunctionCall(responseContent, token);
    }

    protected virtual void RenderStreamingChat(IStreamRender streamingRender, StreamingChatCompletionsUpdate chatUpdate)
    {
        if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
        {
            streamingRender.Refresh(chatUpdate.ContentUpdate);
        }
    }

}

