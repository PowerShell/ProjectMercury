using System.CommandLine;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class RegenCommand : CommandBase
{
    public RegenCommand()
        : base("regen", "Regenerate a new response for the last query.")
    {
        this.SetHandler(RegenAction);
    }

    private void RegenAction()
    {
        var shell = (Shell)Shell;

        if (shell.LastQuery is null)
        {
            shell.Host.MarkupErrorLine($"No previous query available.");
            return;
        }

        shell.Regenerate = true;
    }
}
