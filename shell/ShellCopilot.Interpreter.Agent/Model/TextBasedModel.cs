using Azure.AI.OpenAI;
using ShellCopilot.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellCopilot.Interpreter.Agent;

internal class TextBasedModel : BaseModel
{
    internal TextBasedModel(ChatService chatService, IHost Host, CancellationToken Token) : base(chatService, Host, Token)
    {
    }

    protected override async Task<InternalChatResultsPacket> HandleFunctionCall(string responseContent)
    {
        if (WasCodeGiven(responseContent))
        {
            _chatService.AddResponseToHistory(new ChatRequestAssistantMessage(responseContent));
            return await ExecuteProvidedCode(responseContent);
        }
        else
        {
            _chatService.AddResponseToHistory(new ChatRequestAssistantMessage(responseContent));
            return new InternalChatResultsPacket(responseContent, "No code was given.");
        }
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
        if (startIndex != -1 && endIndex != -1)
        {
            isCodeBlockComplete = true;
        }
        return isCodeBlockComplete;
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
                ToolResponsePacket packet = await host.RunWithSpinnerAsync(func, "Running code...");
                toolMessage = packet.Content;
                host.RenderFullResponse("```\n" + toolMessage + "\n");
            }
        }
        return new InternalChatResultsPacket(responseContent, toolMessage, language, code);
    }

    private string[] ExtractCodeFromResponse(string responseContent)
    {
        string[] extractedCode = ["None", "None"];
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
            string language = codeBlockContent.Substring(0, langLength);
            string code = codeBlockContent.Remove(0, langLength);
            extractedCode = [language, code];
            return extractedCode;
        }
    }

}
