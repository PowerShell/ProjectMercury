using System.CommandLine;
using Spectre.Console;

namespace ShellCopilot.Kernel.Commands;

internal class HelpCommand : CommandBase
{
    private readonly Shell _shell;

    public HelpCommand(Shell shell)
        : base("help", "Show all available commands.")
    {
        _shell = shell;
        this.SetHandler(HelpAction);
    }

    private void HelpAction()
    {
        var commands = _shell.CommandRunner.Commands;
        var list = commands.Values.OrderBy(c => c.Name).ToList();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold white]-[/] Just type your query and press [bold olive underline]Enter[/] to send it to the chosen AI model.");
        AnsiConsole.MarkupLine("[bold white]-[/] To run a command, use the colon prefix [bold olive underline]'/'[/] to indicate command invocation.");
        AnsiConsole.MarkupLine("[bold white]-[/] All available commands are listed in the table below.");

        var elements = new[]
        {
            new RenderElement<CommandBase>(nameof(Name)),
            new RenderElement<CommandBase>(nameof(Description))
        };

        ConsoleRender.RenderTable(list, elements);
    }
}
