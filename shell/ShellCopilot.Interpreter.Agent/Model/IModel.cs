using ShellCopilot.Abstraction;

namespace ShellCopilot.Interpreter.Agent;

public interface IModel
{
    public Task<InternalChatResultsPacket> SmartChat(string input, RenderingStyle renderingStyle, CancellationToken token);
}

