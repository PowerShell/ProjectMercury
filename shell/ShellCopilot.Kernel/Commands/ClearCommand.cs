using System.CommandLine;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class ClearCommand : CommandBase
{
    public ClearCommand()
        : base("cls", "Clear the screen.")
    {
        this.SetHandler(ClearAction);
    }

    private void ClearAction()
    {
        Console.Clear();
    }
}
