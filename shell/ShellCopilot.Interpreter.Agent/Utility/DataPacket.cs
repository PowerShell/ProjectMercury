using Azure.AI.OpenAI;
using System;

namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// This class is used to encapsulate data that is sent between Agent and Computer
/// Using the Azure.AI.OpenAI ChatRole.
/// </summary>
public class DataPacket(ChatRole Role, string Content)
{
    public readonly ChatRole role = Role;
    public string content = Content;
}

public class InternalChatResultsPacket(string AIResponse, string ToolResponse, string language = "", string code = "")
{
    public readonly string aiResponse = AIResponse;
    public readonly bool wasAIResponseEmpty = AIResponse.Contains("No response from AI.");
    public readonly bool isTaskComplete = AIResponse.Contains("The task is done.");
    public readonly bool isTaskImpossible = AIResponse.Contains("The task is impossible.");
    public readonly bool isMoreInformationNeeded = AIResponse.Contains("Please provide more information.");
    public readonly bool isTaskPresent = AIResponse.Contains("Let me know what you'd like to do next.");


    public readonly string toolResponse = ToolResponse;
    public readonly bool wasToolCancelled = ToolResponse.Contains("Tool call was cancelled.");
    public readonly bool wasToolSupported = !ToolResponse.Contains("The tool is not supported.");
    public readonly bool didUserRun = !ToolResponse.Contains("User chose not to run code.");
    public readonly bool languageSupported = !ToolResponse.Contains("Language not supported.");
    public readonly bool languageOnPath = !ToolResponse.Contains("Language not found on path.");
    public readonly bool wasCodeGiven = !ToolResponse.Contains("No code was given.");
    public readonly bool wasThereAnError = ToolResponse.Contains("Error");
    public readonly bool didNotCallTool = ToolResponse.Contains("Tool was not called.");

    public readonly string Language = language;
    public readonly string Code = code;
}
