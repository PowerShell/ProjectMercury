using System.CommandLine;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class LikeCommand : CommandBase
{
    public LikeCommand()
        : base("like", "Like the last response and send feedback.")
    {
        this.SetHandler(LikeAction);
    }

    private void LikeAction()
    {
        var shell = (Shell)Shell;
        Host host = shell.Host;
        try
        {
            bool share = host
                .PromptForConfirmationAsync(
                    prompt: "Great! Would you like to share the conversation history to help further improve the responses?",
                    defaultValue: true,
                    shell.CancellationToken)
                .GetAwaiter().GetResult();

            shell.OnUserAction(new LikePayload(share));
        }
        catch (OperationCanceledException)
        {
            // User pressed 'Ctrl+c', likely because they are just trying out the command.
            host.WriteLine();
        }
    }
}
