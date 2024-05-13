using Azure.AI.OpenAI;
using System.Security.Cryptography;

namespace AISH.Interpreter.Agent;
/// <summary>
/// Summarizes the content of a response from the tool into booleans.
/// </summary>
public class ToolResponsePacket : DataPacket
{
    public bool IsLanguageSupported { get; private set; }
    public bool IsLanguageOnPath { get; private set; }
    public bool Error { get; private set; }
    public readonly string Language;
    public readonly string Code;
    public string ToolId;
    public string Content { get; private set; }

    public ToolResponsePacket(string language, string code) : base(ChatRole.Tool, "")
    {
        Language = language;
        Code = code;
        Content = "";
    }

    public ToolResponsePacket(string content, string language, string code) : base(ChatRole.Tool, content)
    {
        Language = language;
        Code = code;
        Content = "";
        SetContent(content);
    }
    public void SetToolId(string toolId)
    {
        ToolId = toolId;
    }

    public void SetError(bool isError)
    {
        Error = isError;
    }

    public void SetContent(string content)
    {
        Content += content;
        if (Content.StartsWith("Language not supported."))
        {
            IsLanguageSupported = false;
        }
        else
        {
            IsLanguageSupported = true;
        }
        if (Content.StartsWith("Language not found on path."))
        {
            IsLanguageOnPath = false;
        }
        else
        {
            IsLanguageOnPath = true;
        }
    }

    public void ResetContent(string content)
    {
        Content = content;
    }
}
