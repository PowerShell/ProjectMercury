using System.CommandLine;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class RetryCommand : CommandBase
{
    public RetryCommand()
        : base("retry", "Regenerate a new response for the last query.")
    {
        this.SetHandler(RetryAction);
    }

    private void RetryAction()
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
