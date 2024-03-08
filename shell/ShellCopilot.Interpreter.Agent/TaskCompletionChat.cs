using Azure.AI.OpenAI;
using Azure;
using Newtonsoft.Json;
using ShellCopilot.Abstraction;
using System;
using System.Runtime;
using System.Text;
using System.Diagnostics;
namespace ShellCopilot.Interpreter.Agent;
/// <summary>
/// Summary description for Class1
/// </summary>
internal class TaskCompletionChat
{
	private ChatService _chatService;
	private IHost host;
    private Computer computer;
    private CancellationToken token;
    private Dictionary<string,string> prompts = TaskCompletionChatPrompts.prompts;
    private bool isFunctionCallingModel;

	internal TaskCompletionChat(ChatService chatService, IHost Host, CancellationToken Token)
	{
		_chatService = chatService;
		host = Host;
        token = Token;
        computer = new();
	}

    public async Task<bool> StartTask(string input, RenderingStyle _renderingStyle)
    {
        bool chatCompleted = false;
        string previousCode = "";
        input += prompts["Initial"];
        while (!chatCompleted)
        {
            try
            {
                InternalChatResultsPacket packet = await SmartChat(input, _renderingStyle);

                PromptEngineering(ref input, ref chatCompleted, ref previousCode, packet);
            }
            catch (OperationCanceledException)
            {
                chatCompleted = true;
                throw;
            }
        }

        return chatCompleted;
    }

    private void PromptEngineering(ref string input, ref bool chatCompleted, ref string previousCode, InternalChatResultsPacket packet)
    {
        if (packet.wasCodeGiven)
        {
            if(packet.Code.Equals(previousCode) && !string.IsNullOrEmpty(previousCode))
            {
                input = prompts["SameError"];
            }
            else if(packet.didNotCallTool)
            {
                input = prompts["UseTool"];
            }
            else if (packet.wasToolSupported && packet.languageSupported)
            {
                if (packet.didUserRun)
                {
                    if (packet.wasToolCancelled)
                    {
                        input = prompts["ToolCancelled"];
                    }
                    else
                    {
                        if (packet.wasThereAnError)
                        {
                            input = prompts["Error"];
                        }
                        else
                        {
                            input = prompts["Output"] + packet.toolResponse;
                            previousCode = packet.Code;
                        }
                    }
                }
                else
                {
                    input = prompts["Force"];
                }
            }
            else
            {
                input = packet.toolResponse + prompts["Force"];
            }
        }
        else
        {
            if (packet.isTaskComplete)
            {
                //TODO: add a way to save the file
                chatCompleted = true;
                computer.Terminate();
            }
            else if (packet.isTaskImpossible)
            {
                //TODO: add a way to save the file
                chatCompleted = true;
                computer.Terminate();
            }
            else if (packet.isMoreInformationNeeded)
            {
                chatCompleted = true;
            }
            else
            {
                input = prompts["Force"];
            }
        }
    }

    private async Task<InternalChatResultsPacket> SmartChat(string input, RenderingStyle _renderingStyle)
    {
        List<string> executionResult = new();
        Dictionary<int, string> toolCallIdsByIndex = new();
        Dictionary<int, string> functionNamesByIndex = new();
        Dictionary<int, StringBuilder> functionArgumentBuildersByIndex = new();

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
            isFunctionCallingModel = _chatService.IsFunctionCallingModel();
            if (response is not null)
            {
                using var streamingRender = host.NewStreamRender(token);
                try
                {
                    // Cannot pass in `cancellationToken` to `GetChoicesStreaming()` and `GetMessageStreaming()` methods.
                    // Doing so will result in an exception in Azure.Open
                    await foreach (StreamingChatCompletionsUpdate chatUpdate in response)
                    {
                        if (isFunctionCallingModel)
                        {
                            UpdateFunctionToolCallData(toolCallIdsByIndex, 
                                                       functionNamesByIndex, 
                                                       functionArgumentBuildersByIndex, 
                                                       chatUpdate);
                        }
                        if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
                        {
                            streamingRender.Refresh(chatUpdate.ContentUpdate);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ignore the cancellation exception.
                    throw;
                }
                responseContent = streamingRender.AccumulatedContent;
            }
        }

        if (isFunctionCallingModel)
        {
            return await HandleFunctionCall(executionResult, toolCallIdsByIndex, functionNamesByIndex, functionArgumentBuildersByIndex, responseContent);
        }
        else
        {
            if (WasCodeGiven(responseContent))
            {
                _chatService.AddResponseToHistory(new ChatRequestAssistantMessage(responseContent));
                return await ExecuteProvidedCode(responseContent);
            }
        }

        _chatService.AddResponseToHistory(new ChatRequestAssistantMessage(responseContent));
        return new InternalChatResultsPacket(responseContent, "No code was given.");
    }

    private bool WasCodeGiven(string responseContent)
    {
        bool isCodeBlockComplete = false;
        if (string.IsNullOrEmpty(responseContent))
        {
            return isCodeBlockComplete;
        }
        int startIndex = responseContent.IndexOf("```");
        int endIndex = responseContent.IndexOf("```", startIndex + 3);
        if(startIndex != -1 && endIndex != -1)
        {
            isCodeBlockComplete = true;
        }
        return isCodeBlockComplete;
    }

    private string[] ExtractCodeFromResponse(string responseContent)
    {
        string[] extractedCode = ["None","None"];
        int startIndex = responseContent.IndexOf("```");
        int endIndex = responseContent.IndexOf("```", startIndex + 3);

        // Find the first set of backticks
        string codeBlockContent = responseContent.Substring(startIndex + 3, endIndex - startIndex - 3);
        // Exit if code block is empty
        if (string.IsNullOrEmpty(codeBlockContent))
        {
            return extractedCode;
        }
        else
        {
            int langLength = codeBlockContent.IndexOf('\n');
            string language = codeBlockContent.Substring(0,langLength);
            string code = codeBlockContent.Remove(0,langLength);
            extractedCode = [language, code];
            return extractedCode;
        }
    }
    private async Task<InternalChatResultsPacket> ExecuteProvidedCode(string responseContent)
    {
        bool choice = await host.PromptForConfirmationAsync($"\nWould you like to run the code?", true, token);
        string toolMessage = "";
        string language = "";
        string code = "";
        if (!choice)
        {
            toolMessage = "User chose not to run code.";
        }
        else
        {
            string[] langAndCode = ExtractCodeFromResponse(responseContent);
            language = langAndCode[0];
            code = langAndCode[1];
            if (language.Equals("None") && code.Equals("None"))
            {
                toolMessage = "No code was given.";
            }
            else
            {
                Task<ToolResponsePacket> func() => computer.Run(language, code, token);
                ToolResponsePacket packet = await host.RunWithSpinnerAsync(func, "Running the code...");
                toolMessage = packet.Content;
                host.RenderFullResponse("```\n" + toolMessage + "\n");
            }
        }
        return new InternalChatResultsPacket(responseContent, toolMessage, language, code);
    }

    private static void UpdateFunctionToolCallData(Dictionary<int, string> toolCallIdsByIndex, Dictionary<int, string> functionNamesByIndex, Dictionary<int, StringBuilder> functionArgumentBuildersByIndex, StreamingChatCompletionsUpdate chatUpdate)
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

    private async Task<InternalChatResultsPacket> HandleFunctionCall(List<string> executionResult, Dictionary<int, string> toolCallIdsByIndex, Dictionary<int, string> functionNamesByIndex, Dictionary<int, StringBuilder> functionArgumentBuildersByIndex, string responseContent)
    {
        // Start consctructing the assistant message with the response content.
        ChatRequestAssistantMessage assistantHistoryMessage = new(responseContent);
        string toolMessage = "";
        string language = "";
        string code = "";

        if(toolCallIdsByIndex.Count == 0)
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
                host.RenderFullResponse($"```{language}\n{code}\n```");
            }

            // Ask the user if they want to run the code
            bool runChoice = await host.PromptForConfirmationAsync("Would you like to run the code?", true, token);

            if (runChoice)
            {
                // Use the tool
                ToolResponsePacket toolResponse = await UseTool(toolCall, language, code, computer, token);

                if (toolResponse.Content is not null)
                {
                    host.RenderFullResponse($"```{language}:\n{toolResponse.Content}\n```");
                    toolMessage = toolResponse.Content;
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
            _chatService.AddResponseToHistory(new ChatRequestToolMessage(toolMessage, indexIdPair.Value ));
        }

        return new InternalChatResultsPacket(responseContent, toolMessage, language, code);
    }

    private async Task<ToolResponsePacket> UseTool(ChatCompletionsToolCall toolCall, string language, string code, Computer computer, CancellationToken token)
    {
        var functionToolCall = toolCall as ChatCompletionsFunctionToolCall;
        ToolResponsePacket packet = new(language, code);

        if (functionToolCall?.Name == Tools.RunCode.Name)
        {
            // Validate and process the JSON arguments for the function call
            string unvalidatedArguments = functionToolCall.Arguments;
            try
            {
                packet = await computer.Run(language, code, token);
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
