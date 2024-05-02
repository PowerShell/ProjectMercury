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

    internal void Display(Host host, string description = null)
    {
        // Display the description of the agent.
        string desc = description ?? Impl.Description;
        host.MarkupLine(desc.EscapeMarkup());

        // Display properties of the agent.
        if (Impl.AgentInfo is null || Impl.AgentInfo.Count is 0)
        {
            host.WriteLine();
        }
        else
        {
            host.RenderList(Impl.AgentInfo);
        }

        // Display up to 3 sample queries from the agent.
        if (Impl.SampleQueries is not null && Impl.SampleQueries.Count > 0)
        {
            int count = 0;
            host.MarkupLine("[teal]Try one of these sample queries:[/]\n");
            foreach (string query in Impl.SampleQueries)
            {
                count++;
                host.MarkupLine($"  [italic]\"{query.EscapeMarkup()}\"[/]");

                if (count is 3)
                {
                    break;
                }
            }

            host.WriteLine();
        }

        // Display any legal links from the agent.
        if (Impl.LegalLinks is not null && Impl.LegalLinks.Count > 0)
        {
            bool first = true;
            foreach (var pair in Impl.LegalLinks)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    host.Write(" | ");
                }

                host.Markup($"[link={pair.Value.EscapeMarkup()}]{pair.Key.EscapeMarkup()}[/]");
            }

            host.WriteLine("\n");
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
