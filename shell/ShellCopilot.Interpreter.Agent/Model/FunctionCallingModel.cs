using Azure.AI.OpenAI;
using Newtonsoft.Json;
using SharpToken;
using ShellCopilot.Abstraction;
using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShellCopilot.Interpreter.Agent;

internal static class Tools
{

    internal static ChatCompletionsFunctionToolDefinition RunCode = new()
    {
        Name = "execute",
        Description = "This function is able to run given powershell and python code. This will allow you to execute powershell and python code " +
        "on my local machine.",
        Parameters = BinaryData.FromObjectAsJson(
        new
        {
            Type = "object",
            Properties = new
            {
                Language = new
                {
                    Type = "string",
                    Description = "The programming language (required parameter to the `execute` function)",
                    Enum = new[] { "python", "powershell" },
                },
                Code = new
                {
                    Type = "string",
                    Description = "The code to be executed (required parameter to the `execute` function)",
                }
            },
            Required = new[] { "language", "code" }
        },
        new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
    };
}

internal class FunctionCallingModel : BaseModel
{
    private Dictionary<int, string> toolCallIdsByIndex = [];
    private Dictionary<int, string> functionNamesByIndex = [];
    private Dictionary<int, StringBuilder> functionArgumentBuildersByIndex = [];

    internal FunctionCallingModel(ChatService chatService, IHost Host) : base (chatService, Host)
    {
    }

    protected override void RenderStreamingChat(IStreamRender streamingRender, StreamingChatCompletionsUpdate chatUpdate)
    {
        UpdateFunctionToolCallData(chatUpdate);

        if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
        {
            streamingRender.Refresh(chatUpdate.ContentUpdate);
        }
    }

    private void UpdateFunctionToolCallData(StreamingChatCompletionsUpdate chatUpdate)
    {
        if (chatUpdate.ToolCallUpdate is StreamingFunctionToolCallUpdate functionToolCallUpdate)
        {
            if (functionToolCallUpdate.Id != null)
            {
                toolCallIdsByIndex[functionToolCallUpdate.ToolCallIndex] = functionToolCallUpdate.Id;
            }
            if (functionToolCallUpdate.Name != null)
            {
                functionNamesByIndex[functionToolCallUpdate.ToolCallIndex] = functionToolCallUpdate.Name;
            }
            if (functionToolCallUpdate.ArgumentsUpdate != null)
            {
                StringBuilder argumentsBuilder
                    = functionArgumentBuildersByIndex.TryGetValue(
                        functionToolCallUpdate.ToolCallIndex,
                        out StringBuilder existingBuilder) ? existingBuilder : new StringBuilder();
                argumentsBuilder.Append(functionToolCallUpdate.ArgumentsUpdate);
                functionArgumentBuildersByIndex[functionToolCallUpdate.ToolCallIndex] = argumentsBuilder;
            }
        }
    }

    protected override async Task<InternalChatResultsPacket> HandleFunctionCall(string responseContent, CancellationToken token)
    {
        // Start constructing the assistant message with the response content.
        ChatRequestAssistantMessage assistantHistoryMessage = new(responseContent);
        string toolMessage = "";
        string language = "";
        string code = "";

        if (toolCallIdsByIndex.Count == 0)
        {
            _chatService.AddResponseToHistory(assistantHistoryMessage);
            return new InternalChatResultsPacket(responseContent, "Tool was not called.");
        }

        foreach (KeyValuePair<int, string> indexIdPair in toolCallIdsByIndex)
        {
            ChatCompletionsFunctionToolCall toolCall = new ChatCompletionsFunctionToolCall(
                               id: indexIdPair.Value,
                               functionNamesByIndex[indexIdPair.Key],
                               functionArgumentBuildersByIndex[indexIdPair.Key].ToString());

            // Add the tool calls in the assistant message.
            assistantHistoryMessage.ToolCalls.Add(toolCall);

            // Add it to the history
            _chatService.AddResponseToHistory(assistantHistoryMessage);

            string arguments = toolCall.Arguments;

            // Extract the language and code from the arguments
            if (arguments != null)
            {
                Dictionary<string, string> argumentsDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);
                language = argumentsDict["language"];
                code = argumentsDict["code"];
                host.RenderFullResponse($"```{language}\n\n{language}:\n\n{code}\n\n```");
            }

            // Ask the user if they want to run the code
            try
            {
                bool runChoice = await host.PromptForConfirmationAsync("Would you like to run the code?", true, token);

                if (runChoice)
                {
                    // Use the tool
                    ToolResponsePacket toolResponse = await UseTool(toolCall, language, code, computer, token);

                    // Reduce code output in chat history as needed
                    if (toolResponse.Content is not null)
                    {
                        host.RenderFullResponse($"```\n\n{language} output:\n\n{toolResponse.Content}\n\n```");
                        toolMessage = _chatService.ReduceToolResponseContentTokens(toolResponse.Content);
                    }
                    else
                    {
                        toolMessage = "Tool response was null";
                    }
                }
                else
                {
                    toolMessage = "User chose not to run code.";
                }
            }
            catch (OperationCanceledException)
            {
                toolMessage = "User chose not to run code.";
                _chatService.AddResponseToHistory(new ChatRequestToolMessage(toolMessage, indexIdPair.Value));
                return new InternalChatResultsPacket(responseContent, toolMessage, language, code);
            }
            _chatService.AddResponseToHistory(new ChatRequestToolMessage(toolMessage, indexIdPair.Value));
        }

        // Clear the tool call data.
        toolCallIdsByIndex.Clear();
        functionNamesByIndex.Clear();
        functionArgumentBuildersByIndex.Clear();

        return new InternalChatResultsPacket(responseContent, toolMessage, language, code);
    }

    private async Task<ToolResponsePacket> UseTool(ChatCompletionsToolCall toolCall, string language, string code, Computer computer, CancellationToken token)
    {
        var functionToolCall = toolCall as ChatCompletionsFunctionToolCall;
        ToolResponsePacket packet = new(language, code);

        if (functionToolCall?.Name == Tools.RunCode.Name)
        {
            try
            {
                Task<ToolResponsePacket> func_run_code() => computer.Run(language, code, token);
                packet = await host.RunWithSpinnerAsync(func_run_code, "Running Code...").ConfigureAwait(false);
                packet.SetToolId(toolCall.Id);
            }
            catch (OperationCanceledException)
            {
                // Ignore the cancellation exception.
                packet.SetContent("Tool call was cancelled.");
            }
        }
        else
        {
            // Handle other or unexpected calls
            packet.SetContent("The tool is not supported.");
        }
        return packet;
    }
}

