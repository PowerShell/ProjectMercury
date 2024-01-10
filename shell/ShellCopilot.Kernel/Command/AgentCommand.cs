using System.CommandLine;
using System.CommandLine.Completions;
using System.Diagnostics;
using ShellCopilot.Abstraction;
using Spectre.Console;

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

        AddCommand(use);
        AddCommand(pop);
        AddCommand(config);
    }

    private void UseAgentAction(string name)
    {
        var shell = (Shell)Shell;
        LLMAgent chosenAgent = string.IsNullOrEmpty(name)
            ? shell.Host.PromptForSelectionAsync(
                title: "[orange1]Please select an [Blue]agent[/] to use[/]:",
                choices: shell.Agents,
                converter: AgentName).GetAwaiter().GetResult()
            : FindAgent(name, shell);

        if (chosenAgent is null)
        {
            AgentNotFound(name, shell);
            return;
        }

        var current = chosenAgent.Impl;
        shell.SwitchActiveAgent(chosenAgent);
        shell.Host.MarkupLine($"Using the agent [green]{current.Name}[/]:\n[italic]{current.Description.EscapeMarkup()}[/]\n");
    }

    private void PopAgentAction()
    {
        var shell = (Shell)Shell;

        try
        {
            shell.PopActiveAgent();

            var current = shell.ActiveAgent.Impl;
            shell.Host.MarkupLine($"Using the agent [green]{current.Name}[/]:\n[italic]{current.Description.EscapeMarkup()}[/]\n");
        }
        catch (Exception ex)
        {
            shell.Host.MarkupErrorLine(ex.Message);
        }
    }

    private void ConfigAgentAction(string name, string editor)
    {
        var shell = (Shell)Shell;
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
            shell.Host.MarkupErrorLine($"The agent '{current.Name}' doesn't support configuration.");
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
            shell.Host.MarkupErrorLine(ex.Message);
        }
    }

    private static string AgentName(LLMAgent agent)
    {
        return agent.Impl.Name;
    }

    private static LLMAgent FindAgent(string name, Shell shell)
    {
        return shell.Agents.FirstOrDefault(a => string.Equals(name, a.Impl.Name));
    }

    private static void AgentNotFound(string name, Shell shell)
    {
        string availableAgentNames = string.Join(',', shell.Agents.Select(AgentName));
        shell.Host.MarkupErrorLine($"Cannot find an agent with the name '{name}'. Available agent(s): {availableAgentNames}");
    }

    private IEnumerable<string> AgentCompleter(CompletionContext context)
    {
        var shell = (Shell)Shell;
        return shell.Agents.Select(AgentName);
    }
}
