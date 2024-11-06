using System.CommandLine;
using AIShell.Abstraction;

namespace AIShell.Kernel.Commands;

internal sealed class RefreshCommand : CommandBase
{
    public RefreshCommand()
        : base("refresh", "Start a new chat session.")
    {
        this.SetHandler(RefreshAction);
    }

    private void RefreshAction()
    {
        Console.Write("\x1b[2J");
        Console.SetCursorPosition(0, Console.WindowTop);

        var shell = (Shell)Shell;
        shell.ShowBanner();
        shell.ShowLandingPage();
        shell.ActiveAgent.Impl.RefreshChatAsync(Shell, force: true).GetAwaiter().GetResult();
    }
}
