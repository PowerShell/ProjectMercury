using System.CommandLine;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class ExitCommand : CommandBase
{
    public ExitCommand()
        : base("exit", "Exit the interactive session.")
    {
        this.SetHandler(ExitAction);
    }

    private void ExitAction()
    {
        var shellImpl = (Shell)Shell;
        shellImpl.Exit = true;
    }
}
