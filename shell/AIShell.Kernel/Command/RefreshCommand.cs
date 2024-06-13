using System.CommandLine;
using AIShell.Abstraction;

namespace AIShell.Kernel.Commands;

internal sealed class RefreshCommand : CommandBase
{
    public RefreshCommand()
        : base("refresh", "Refresh the chat session.")
    {
        this.SetHandler(RefreshAction);
    }

    private void RefreshAction()
    {
        Console.Write("\x1b[2J");
        Console.SetCursorPosition(0, Console.WindowTop);

        var shell = (Shell)Shell;
        shell.ActiveAgent.Impl.RefreshChat();
        shell.ShowBanner();
        shell.ShowLandingPage();
    }
}
