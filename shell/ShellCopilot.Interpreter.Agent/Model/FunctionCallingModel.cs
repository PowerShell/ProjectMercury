using Azure.AI.OpenAI;
using ShellCopilot.Abstraction;
using System.Text;
using System.Text.Json;

namespace ShellCopilot.Interpreter.Agent;

internal static class Tools
{

    internal static ChatCompletionsFunctionToolDefinition RunCode = new()
    {
        Name = "execute",
        Description = "This function is able to run given PowerShell and Python code. This will allow you to execute PowerShell and Python code " +
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

    internal FunctionCallingModel(bool autoExecution, 
                                  bool displayErrors, 
                                  ChatService chatService, 
                                  CodeExecutionService executionService,
                                  IHost Host) : base (autoExecution, displayErrors, chatService, executionService, Host)
    {
    }

    protected override void RenderStreamingChat(IStreamRender streamingRender, StreamingChatCompletionsUpdate chatUpdate)
    {
        // 'chatUpdate' contains either 'ToolCallUpdate' or 'ContentUpdate', but never both.
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
            ChatService.AddResponseToHistory(assistantHistoryMessage);
            return new InternalChatResultsPacket(responseContent, "No code was given.");
        }

        foreach (KeyValuePair<int, string> indexIdPair in toolCallIdsByIndex)
        {
            ChatCompletionsFunctionToolCall toolCall = new ChatCompletionsFunctionToolCall(
                               id: indexIdPair.Value,
                               functionNamesByIndex[indexIdPair.Key],
                               functionArgumentBuildersByIndex[indexIdPair.Key].ToString());

            // Add the tool calls in the assistant message.
            assistantHistoryMessage.ToolCalls.Add(toolCall);
        }

        // Add it to the history
        ChatService.AddResponseToHistory(assistantHistoryMessage);

        foreach (ChatCompletionsFunctionToolCall toolCall in assistantHistoryMessage.ToolCalls.Cast<ChatCompletionsFunctionToolCall>())
        {
            string arguments = toolCall.Arguments;

            // Extract the language and code from the arguments
            if (arguments != null)
            {
                Dictionary<string, string> argumentsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(arguments);
                language = argumentsDict["language"];
                code = argumentsDict["code"];
                Host.RenderFullResponse($"```{language}\n\n# {language}\n\n{code}\n\n```");
            }

            // Ask the user if they want to run the code
            try
            {
                bool runChoice;
                if (AutoExecution)
                {
                    runChoice = true;
                }
                else
                {
                    // Prompt the user to run the code (if not in auto execution mode
                    runChoice = await Host.PromptForConfirmationAsync("Would you like to run the code? Select 'n' to provide more guidance, the process state will be saved.", true, token);
                }

                if (runChoice)
                {
                    // Use the tool
                    ToolResponsePacket toolResponse = await UseTool(toolCall, language, code, token);

                    // Reduce code output in chat history as needed
                    if (toolResponse.Content is not null)
                    {
                        if (!DisplayErrors)
                        {
                            if (!toolResponse.Error)
                            {
                                Host.RenderFullResponse($"```\n\n{language} output:\n\n{toolResponse.Content}\n\n```");
                            }
                        }
                        else
                        {
                            Host.RenderFullResponse($"```\n\n{language} output:\n\n{toolResponse.Content}\n\n```");
                        }
                        toolMessage = ChatService.ReduceToolResponseContentTokens(toolResponse.Content);
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
                toolMessage = "Tool call was cancelled.";
                ChatService.AddResponseToHistory(new ChatRequestToolMessage(toolMessage, toolCall.Id));
                ClearToolData();
                return new InternalChatResultsPacket(responseContent, toolMessage, language, code);
            }
            ChatService.AddResponseToHistory(new ChatRequestToolMessage(toolMessage, toolCall.Id));
        }

        // Clear the tool call data.
        ClearToolData();

        return new InternalChatResultsPacket(responseContent, toolMessage, language, code);
    }

    private void ClearToolData()
    {
        toolCallIdsByIndex.Clear();
        functionNamesByIndex.Clear();
        functionArgumentBuildersByIndex.Clear();
    }

    private async Task<ToolResponsePacket> UseTool(ChatCompletionsToolCall toolCall, string language, string code, CancellationToken token)
    {
        var functionToolCall = toolCall as ChatCompletionsFunctionToolCall;
        ToolResponsePacket packet = new(language, code);

        if (functionToolCall?.Name == Tools.RunCode.Name)
        {
            try
            {
                Task<ToolResponsePacket> func_run_code() => ExecutionService.Run(language, code, token);
                packet = await Host.RunWithSpinnerAsync(func_run_code, "Running Code...").ConfigureAwait(false);
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

