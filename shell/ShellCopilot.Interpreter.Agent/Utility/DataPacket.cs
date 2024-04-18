using Azure.AI.OpenAI;
using System;

namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// This class is used to encapsulate data that is sent between Agent and CodeExeuctionService.
/// Using the Azure.AI.OpenAI ChatRole.
/// </summary>
public class DataPacket(ChatRole Role, string Content)
{
    public readonly ChatRole role = Role;
    public string content = Content;
}

public class InternalChatResultsPacket(string AIResponse, string ToolResponse, string language = "", string code = "")
{
    public readonly string aiResponse = AIResponse ?? "No response from AI.";
    public readonly bool wasResponseCancelled = AIResponse?.Contains("AI response cancelled.") ?? false;
    public readonly bool wasAIResponseEmpty = AIResponse?.Contains("No response from AI.") ?? false;
    public readonly bool isTaskComplete = 
        AIResponse?.IndexOf("The task is done.", StringComparison.OrdinalIgnoreCase) >= 0;
    public readonly bool isTaskImpossible = 
        AIResponse?.IndexOf("The task is impossible.", StringComparison.OrdinalIgnoreCase) >= 0;
    public readonly bool isMoreInformationNeeded =
        AIResponse?.IndexOf("Please provide more information.", StringComparison.OrdinalIgnoreCase) >= 0;
    public readonly bool isNoTaskPresent =
        AIResponse?.IndexOf("Let me know what you'd like to do next.", StringComparison.OrdinalIgnoreCase) >= 0;

    public string toolResponse = ToolResponse;
    public readonly bool wasToolCancelled = ToolResponse.Contains("Tool call was cancelled.");
    public readonly bool wasToolSupported = !ToolResponse.Contains("The tool is not supported.");
    public readonly bool didUserRun = !ToolResponse.Contains("User chose not to run code.");
    public readonly bool languageSupported = !ToolResponse.Contains("Language not supported.");
    public readonly bool languageOnPath = !ToolResponse.Contains("Language not found on path.");
    public readonly bool wasCodeGiven = !ToolResponse.Contains("No code was given.");
    public bool wasThereAnError = ToolResponse.Contains("Error");
    public readonly bool didNotCallTool = ToolResponse.Contains("Tool was not called.");

    public readonly string Language = language;
    public readonly string Code = code;

    public void SetToolResponse(string response)
    {
        toolResponse = response;
    }

    public void SetError(bool error)
    {
        wasThereAnError = error;
    }
}
