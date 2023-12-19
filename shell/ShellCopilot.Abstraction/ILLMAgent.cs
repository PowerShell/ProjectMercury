
namespace ShellCopilot.Abstraction;

public enum RenderingStyle
{
    FullResponsePreferred,
    StreamingResponsePreferred,
}

public class AgentConfig
{
    public string ConfigurationRoot { set; get; }
    public RenderingStyle RenderingStyle { set; get; }
}

public interface ILLMAgent
{
    string Name { get; }
    string Description { get; }
    string SettingFile { get; }

    void Initialize(AgentConfig config);
    Task Chat(string input, IShellContext shell);
}

public interface IOrchestrator : ILLMAgent
{
    int FindAgentForPrompt(string prompt, List<string> agents);
}

public interface ICodeAnalyzer : ILLMAgent
{
    bool AnalyzeCode(List<string> codeBlocks, IShellContext shell, out string explanation);
}

internal static class LLMAgentExtension
{
    private static bool Is<T>(ILLMAgent obj, out T result) where T : ILLMAgent
    {
        if (obj is T value)
        {
            result = value;
            return true;
        }

        result = default;
        return false;
    }

    internal static bool IsOrchestrator(this ILLMAgent agent, out IOrchestrator orchestrator)
    {
        return Is<IOrchestrator>(agent, out orchestrator);
    }

    internal static bool IsCodeAnalyzer(this ILLMAgent agent, out ICodeAnalyzer analyzer)
    {
        return Is<ICodeAnalyzer>(agent, out analyzer);
    }
}
