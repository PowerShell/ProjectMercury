using System.CommandLine;
using ShellCopilot.Abstraction;
using Spectre.Console;

namespace ShellCopilot.Kernel.Commands;

internal sealed class HelpCommand : CommandBase
{
    public HelpCommand()
        : base("help", "Show all available commands.")
    {
        this.SetHandler(HelpAction);
    }

    private void HelpAction()
    {
        var shellImpl = (Shell)Shell;
        var commands = shellImpl.CommandRunner.Commands;
        var list = commands.Values.OrderBy(c => c.Name).ToList();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold white]-[/] Just type your query and press [bold olive underline]Enter[/] to chat with the chosen AI agent.");
        AnsiConsole.MarkupLine("[bold white]-[/] To run a command, use the prefix [bold olive underline]'/'[/] to indicate command invocation.");
        AnsiConsole.MarkupLine("[bold white]-[/] All available commands are listed in the table below.");

        var elements = new[]
        {
            new PropertyElement<CommandBase>(nameof(Name)),
            new PropertyElement<CommandBase>(nameof(Description))
        };

        shellImpl.Host.RenderTable(list, elements);
    }
}
