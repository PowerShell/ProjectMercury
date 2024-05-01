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

    public string toolResponse = ToolResponse;
    public readonly bool wasToolCancelled = ToolResponse.Contains("Tool call was cancelled.");
    public readonly bool didUserRun = !ToolResponse.Contains("User chose not to run code.");
    public readonly bool wasCodeGiven = !ToolResponse.Contains("No code was given.");
    public bool wasThereAnError = ToolResponse.Contains("Error");

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
