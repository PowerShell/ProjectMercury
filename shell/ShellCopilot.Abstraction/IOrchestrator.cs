namespace ShellCopilot.Abstraction;

public interface IOrchestrator : IChatService
{
    int FindAgentForPrompt(string prompt, List<string> agents);
}
