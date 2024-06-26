﻿using System.CommandLine;
using AIShell.Abstraction;

namespace AIShell.Kernel.Commands;

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
            shell.Host.WriteErrorLine("No previous query available.");
            return;
        }

        shell.Regenerate = true;
        shell.OnUserAction(new RetryPayload(shell.LastQuery));
    }
}
