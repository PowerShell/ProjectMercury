using System.CommandLine;
using ShellCopilot.Abstraction;
using Spectre.Console;

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

        if (shell.LastAgent is null)
        {
            host.WriteErrorLine("No previous response available to rate on.");
            return;
        }

        try
        {
            host.MarkupLine("[cyan]Great! Thank you for the feedback![/]");
            string prompt = $"[cyan]{GetPromptForHistorySharing(shell.LastAgent.Impl)}[/]";
            bool share = host
                .PromptForConfirmationAsync(
                    prompt: prompt,
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

    internal static string GetPromptForHistorySharing(ILLMAgent agent)
    {
        string product = agent.Company is null
            ? $"the agent [green]{agent.Name}[/]"
            : $"{agent.Company} products and services";

        string privacy = null;
        if (agent.LegalLinks is not null)
        {
            string privacyText = "Privacy statement";
            bool hasLink = agent.LegalLinks.TryGetValue(privacyText, out string link);
            if (!hasLink)
            {
                privacyText = "Privacy";
                hasLink = agent.LegalLinks.TryGetValue(privacyText, out link);
            }

            privacy = hasLink ? $" ([link={link.EscapeMarkup()}]{privacyText.EscapeMarkup()}[/])" : null;
        }

        return $"Would you like to share the conversation history to help further improve {product}?{privacy}";
    }
}
