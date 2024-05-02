using System.CommandLine;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class DislikeCommand : FeedbackCommand
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
            host.WriteLine("Thanks for your feedback. Please share more about your experience.\n");
            string shortFeedback = host
                .PromptForSelectionAsync("Was the response:", _choices, cancellationToken: shell.CancellationToken)
                .GetAwaiter()
                .GetResult();

            host.MarkupLine($"The response was: [teal]{shortFeedback}[/]\n");
            string longFeedback = host
                .PromptForTextAsync("What went wrong? ", optional: true, shell.CancellationToken)
                .GetAwaiter()
                .GetResult();

            host.WriteLine();
            string prompt = GetPromptForHistorySharing(shell.LastAgent.Impl);
            bool share = AskForHistorySharingAsync(prompt, shell.CancellationToken)
                .GetAwaiter().GetResult();

            host.WriteLine("\nThanks again for your feedback!\n");
            shell.OnUserAction(new DislikePayload(share, shortFeedback, longFeedback));
        }
        catch (OperationCanceledException)
        {
            // User pressed 'Ctrl+c', likely because they are just trying out the command.
            host.WriteLine("\n");
        }
    }
}
