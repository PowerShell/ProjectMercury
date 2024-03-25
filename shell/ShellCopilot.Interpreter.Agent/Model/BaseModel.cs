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
    internal ChatService _chatService;
    internal IHost host;
    internal Computer computer;

    protected abstract Task<InternalChatResultsPacket> HandleFunctionCall(string responseContent, CancellationToken token);

    internal BaseModel(ChatService chatService, IHost Host)
    {
        _chatService = chatService;
        host = Host;
        computer = new Computer();
    }

    public async Task<InternalChatResultsPacket> SmartChat(string input, RenderingStyle _renderingStyle, CancellationToken token)
    {
        string responseContent = null;
        if (_renderingStyle is RenderingStyle.FullResponsePreferred)
        {
            // TODO: Add a way to handle the response if it is a tool call
            // TODO: Test FullResponsePreferred
            ChatRequestUserMessage chatRequestUserMessage = new(input);
            Task<Response<ChatCompletions>> func_non_streaming() => _chatService.GetChatCompletionsAsync(chatRequestUserMessage, token);
            Response<ChatCompletions> response = await host.RunWithSpinnerAsync(func_non_streaming).ConfigureAwait(false);

            if (response is not null)
            {
                ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
                host.RenderFullResponse(responseContent);

                ChatChoice responseChoice = response.Value.Choices[0];
                if (responseChoice.FinishReason is CompletionsFinishReason FinishReason)
                {
                    //TODO need to rework. This is a temporary fix
                    string warning = "";
                    if (warning is not null)
                    {
                        host.MarkupWarningLine(warning);
                        host.WriteLine();
                    }
                }
                // _chatService.AddToolCallToHistory(response);
            }
        }
        else
        {
            ChatRequestUserMessage chatRequestUserMessage = new(input);
            Task<StreamingResponse<StreamingChatCompletionsUpdate>> func_streaming() => _chatService.GetStreamingChatResponseAsync(chatRequestUserMessage, token);
            StreamingResponse<StreamingChatCompletionsUpdate> response = await host.RunWithSpinnerAsync(func_streaming).ConfigureAwait(false);
            if (response is not null)
            {
                using var streamingRender = host.NewStreamRender(token);
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

