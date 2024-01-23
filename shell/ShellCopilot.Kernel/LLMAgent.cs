using ShellCopilot.Abstraction;
using Spectre.Console;

namespace ShellCopilot.Kernel;

internal class LLMAgent
{
    internal ILLMAgent Impl { get; }
    internal AgentAssemblyLoadContext LoadContext { get; }
    internal bool OrchestratorRoleDisabled { set; get; }
    internal bool AnalyzerRoleDisabled { set; get; }

    internal LLMAgent(ILLMAgent agent, AgentAssemblyLoadContext loadContext)
    {
        Impl = agent;
        LoadContext = loadContext;

        OrchestratorRoleDisabled = false;
        AnalyzerRoleDisabled = false;
    }

    internal void Display(Host host)
    {
        host.MarkupLine($"[italic]{Impl.Description.EscapeMarkup()}[/]");
        if (Impl.AgentInfo is null || Impl.AgentInfo.Count is 0)
        {
            host.WriteLine();
        }
        else
        {
            host.RenderList(Impl.AgentInfo);
        }
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
