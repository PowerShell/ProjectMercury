using ShellCopilot.Abstraction;

namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// Interface for different LLM models. 
/// </summary>
public interface IModel
{
    /// <summary>
    /// Calls the API and renders chat completions. Then it calls the HandleFunctionCall method to handle the response.
    /// </summary>
    public Task<InternalChatResultsPacket> SmartChat(string input, RenderingStyle renderingStyle, CancellationToken token);
}

