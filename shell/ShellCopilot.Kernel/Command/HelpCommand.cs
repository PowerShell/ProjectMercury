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
        var host = shellImpl.Host;

        var commands = shellImpl.CommandRunner.Commands;
        var list = commands.Values.OrderBy(c => c.Name).ToList();

        host.WriteLine();
        host.MarkupLine("[bold white]-[/] Type then press [bold olive underline]Enter[/] to chat with the chosen AI agent.");
        host.MarkupLine("[bold white]-[/] All available commands are listed in the table below.");

        var elements = new IRenderElement<CommandBase>[]
        {
            new CustomElement<CommandBase>("Name", c => $"/{c.Name}"),
            new PropertyElement<CommandBase>(nameof(Description))
        };

        shellImpl.Host.RenderTable(list, elements);
        host.MarkupLine($"Learn more at [link]https://aka.ms/CopilotforShell[/].\n");
    }
}
