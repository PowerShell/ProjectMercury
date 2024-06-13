using System.CommandLine;
using AIShell.Abstraction;

namespace AIShell.Kernel.Commands;

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
