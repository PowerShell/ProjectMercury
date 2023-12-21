using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel;

internal class LLMAgent
{
    internal ILLMAgent Impl { get; }
    internal bool SelfCheckSucceeded { set; get; }
    internal bool OrchestratorRoleDisabled { set; get; }
    internal bool AnalyzerRoleDisabled { set; get; }

    internal LLMAgent(ILLMAgent agent)
    {
        Impl = agent;

        SelfCheckSucceeded = false;
        OrchestratorRoleDisabled = false;
        AnalyzerRoleDisabled = false;
    }

    internal bool IsOrchestrator(out IOrchestrator orchestrator)
    {
        return Is(Impl, out orchestrator);
    }

    internal bool IsCodeAnalyzer(out ICodeAnalyzer analyzer)
    {
        return Is(Impl, out analyzer);
    }

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
}
