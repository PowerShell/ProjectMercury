using System.CommandLine;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class DislikeCommand : CommandBase
{
    private readonly List<string> _choices;

    public DislikeCommand()
        : base("dislike", "Dislike the last response and send feedback.")
    {
        _choices = ["Inaccurate", "Irrelevant", "Offensive or inappropriate", "Too slow to response", "Other"];
        this.SetHandler(DislikeAction);
    }

    private void DislikeAction()
    {
        var shell = (Shell)Shell;
        Host host = shell.Host;

        if (shell.LastAgent is null)
        {
            host.WriteErrorLine("No previous response available to rate on.");
            return;
        }

        try
        {
            host.MarkupLine("[cyan]Thanks for your feedback. Please share more about your experience.[/]\n");
            string shortFeedback = host
                .PromptForSelectionAsync("[cyan]Was the response:[/]", _choices, cancellationToken: shell.CancellationToken)
                .GetAwaiter()
                .GetResult();

            string longFeedback = host
                .PromptForTextAsync("[cyan]What went wrong?[/] ", optional: true, shell.CancellationToken)
                .GetAwaiter()
                .GetResult();

            host.WriteLine();
            bool share = host
                .PromptForConfirmationAsync(
                    prompt: "[cyan]Would you like to share the conversation history to help further improve the responses?[/]",
                    defaultValue: true,
                    shell.CancellationToken)
                .GetAwaiter().GetResult();

            host.MarkupLine("[cyan]Thanks again for your feedback![/]");
            shell.OnUserAction(new DislikePayload(share, shortFeedback, longFeedback));
        }
        catch (OperationCanceledException)
        {
            // User pressed 'Ctrl+c', likely because they are just trying out the command.
            host.WriteLine();
        }
    }
}
