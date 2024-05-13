using System.CommandLine;
using AISH.Abstraction;

namespace AISH.Kernel.Commands;

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
