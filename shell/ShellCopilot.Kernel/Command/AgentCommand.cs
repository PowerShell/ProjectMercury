using System.CommandLine;
using System.CommandLine.Completions;
using System.Diagnostics;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class AgentCommand : CommandBase
{
    public AgentCommand()
        : base("agent", "Command for agent management.")
    {
        var use = new Command("use", "Specify an agent to use, or choose one from the available agents.");
        var useAgent = new Argument<string>(
            name: "agent",
            getDefaultValue: () => null,
            description: "Name of an agent.").AddCompletions(AgentCompleter);
        use.AddArgument(useAgent);
        use.SetHandler(UseAgentAction, useAgent);

        var pop = new Command("pop", "Pop the current active agent off the stack and go back to the orchestrator agent.");
        pop.SetHandler(PopAgentAction);

        var config = new Command("config", "Open up the setting file for an agent. When no agent is specified, target the active agent.");
        var editor = new Option<string>("--editor", "The editor to open the setting file in.");
        var configAgent = new Argument<string>(
            name: "agent",
            getDefaultValue: () => null,
            description: "Name of an agent.").AddCompletions(AgentCompleter);
        config.AddArgument(configAgent);
        config.AddOption(editor);
        config.SetHandler(ConfigAgentAction, configAgent, editor);

        var list = new Command("list", "List all available agents.");
        list.SetHandler(ListAgentAction);

        AddCommand(config);
        AddCommand(list);
        AddCommand(pop);
        AddCommand(use);
    }

    private void ListAgentAction()
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        if (!HasAnyAgent(shell, host))
        {
            return;
        }

        var active = shell.ActiveAgent;
        var list = shell.Agents;

        var elements = new IRenderElement<LLMAgent>[]
        {
            new CustomElement<LLMAgent>("Name", c => c == active ? $"{c.Impl.Name} (active)" : c.Impl.Name),
            new CustomElement<LLMAgent>("Description", c => c.Impl.Description),
        };

        host.RenderTable(list, elements);
    }

    private void UseAgentAction(string name)
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        if (!HasAnyAgent(shell, host))
        {
            return;
        }

        LLMAgent chosenAgent = string.IsNullOrEmpty(name)
            ? host.PromptForSelectionAsync(
                title: "[orange1]Please select an [Blue]agent[/] to use[/]:",
                choices: shell.Agents,
                converter: AgentName).GetAwaiter().GetResult()
            : FindAgent(name, shell);

        if (chosenAgent is null)
        {
            AgentNotFound(name, shell);
            return;
        }

        shell.SwitchActiveAgent(chosenAgent);
        host.MarkupLine($"Using the agent [green]{chosenAgent.Impl.Name}[/]:");
        chosenAgent.Display(host);
    }

    private void PopAgentAction()
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        try
        {
            shell.PopActiveAgent();

            var current = shell.ActiveAgent;
            host.MarkupLine($"Using the agent [green]{current.Impl.Name}[/]:");
            current.Display(host);
        }
        catch (Exception ex)
        {
            host.WriteErrorLine(ex.Message);
        }
    }

    private void ConfigAgentAction(string name, string editor)
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        if (!HasAnyAgent(shell, host))
        {
            return;
        }

        LLMAgent chosenAgent = string.IsNullOrEmpty(name)
            ? shell.ActiveAgent
            : FindAgent(name, shell);

        if (chosenAgent is null)
        {
            AgentNotFound(name, shell);
            return;
        }

        var current = chosenAgent.Impl;
        if (current.SettingFile is null)
        {
            host.WriteErrorLine($"The agent '{current.Name}' doesn't support configuration.");
            return;
        }

        if (string.IsNullOrEmpty(editor))
        {
            editor = OperatingSystem.IsWindows() ? "notepad" : "nano";
        }

        try
        {
            ProcessStartInfo info = new(editor, current.SettingFile);
            Process.Start(info).WaitForExit();
        }
        catch (Exception ex)
        {
            host.WriteErrorLine(ex.Message);
        }
    }

    private static bool HasAnyAgent(Shell shell, Host host)
    {
        if (shell.Agents.Count is 0)
        {
            host.WriteErrorLine("No agent is available.");
            return false;
        }

        return true;
    }

    private static string AgentName(LLMAgent agent)
    {
        return agent.Impl.Name;
    }

    private static LLMAgent FindAgent(string name, Shell shell)
    {
        return shell.Agents.FirstOrDefault(a => string.Equals(name, a.Impl.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static void AgentNotFound(string name, Shell shell)
    {
        string availableAgentNames = string.Join(", ", shell.Agents.Select(AgentName));
        shell.Host.WriteErrorLine($"Cannot find an agent with the name '{name}'. Available agent(s): {availableAgentNames}.");
    }

    private IEnumerable<string> AgentCompleter(CompletionContext context)
    {
        var shell = (Shell)Shell;
        return shell.Agents.Select(AgentName);
    }
}
