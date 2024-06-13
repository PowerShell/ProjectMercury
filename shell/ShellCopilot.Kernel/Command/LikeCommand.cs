using System.CommandLine;
using AIShell.Abstraction;
using Spectre.Console;

namespace AIShell.Kernel.Commands;

internal abstract class FeedbackCommand : CommandBase
{
    protected FeedbackCommand(string name, string description)
        : base(name, description)
    {
    }

    protected static string GetPromptForHistorySharing(ILLMAgent agent)
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

    /// <summary>
    /// The `TextPrompt.ChoiceStyle` doesn't work with `Style.Plain`, which looks like a bug.
    /// So, we have to build the choices and default value into the prompt.
    /// </summary>
    protected static async Task<bool> AskForHistorySharingAsync(string promptText, CancellationToken cancellationToken)
    {
        var comparer = StringComparer.CurrentCultureIgnoreCase;

        var prompt = new TextPrompt<char>(promptText, comparer)
            .PromptStyle(new Style(Color.Teal))
            .InvalidChoiceMessage("[red]Please select one of the available options[/]")
            .ValidationErrorMessage("[red]Please select one of the available options[/]")
            .DefaultValue('y')
            .AddChoice('y')
            .AddChoice('n');

        var result = await prompt.ShowAsync(AnsiConsole.Console, cancellationToken).ConfigureAwait(false);
        return comparer.Compare('y'.ToString(), result.ToString()) == 0;
    }
}

internal sealed class LikeCommand : FeedbackCommand
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
            host.WriteLine("Great! Thank you for the feedback!\n");
            string prompt = GetPromptForHistorySharing(shell.LastAgent.Impl);
            bool share = AskForHistorySharingAsync(prompt, shell.CancellationToken)
                .GetAwaiter().GetResult();

            host.WriteLine();
            shell.OnUserAction(new LikePayload(share));
        }
        catch (OperationCanceledException)
        {
            // User pressed 'Ctrl+c', likely because they are just trying out the command.
            host.WriteLine("\n");
        }
    }
}
