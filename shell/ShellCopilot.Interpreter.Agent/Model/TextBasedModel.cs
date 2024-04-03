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
    internal TextBasedModel(bool autoExecution, ChatService chatService, IHost Host) : base(autoExecution, chatService, Host)
    {
    }

    protected override async Task<InternalChatResultsPacket> HandleFunctionCall(string responseContent, CancellationToken token)
    {
        InternalChatResultsPacket packet;
        if (WasCodeGiven(responseContent))
        {
            ChatService.AddResponseToHistory(new ChatRequestAssistantMessage(responseContent));

            bool runChoice;
            if (AutoExecution)
            {
                runChoice = true;
            }
            else
            {
                // Prompt the user to run the code (if not in auto execution mode
                runChoice = await Host.PromptForConfirmationAsync("Would you like to run the code?", true, token);
            }
            
            if (runChoice)
            {
                packet = await ExecuteProvidedCode(responseContent, token);
            }
            else
            {
                packet = new InternalChatResultsPacket(responseContent, "User chose not to run code.");
            }
        }
        else
        {
            ChatService.AddResponseToHistory(new ChatRequestAssistantMessage(responseContent));
            packet = new InternalChatResultsPacket(responseContent, "No code was given.");
        }
        return packet;
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
            if(responseContent.Contains("```python\n") || responseContent.Contains("```powershell\n"))
            {
                isCodeBlockComplete = true;
            }
        }
        return isCodeBlockComplete;
    }
    private async Task<InternalChatResultsPacket> ExecuteProvidedCode(string responseContent, CancellationToken token)
    {
        string toolMessage = "";
        string language = "";
        string code = "";

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
            ToolResponsePacket toolResponse = await Host.RunWithSpinnerAsync(func, "Running code...");
            Host.RenderFullResponse("```\n" + toolResponse.Content + "\n");
            toolMessage = ChatService.ReduceToolResponseContentTokens(toolResponse.Content);
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
