
using Spectre.Console;

namespace ShellCopilot.Kernel;

internal class Host
{
    private readonly bool _outputRedirected;
    private readonly bool _errorRedirected;

    internal Host()
    {
        _outputRedirected = Console.IsOutputRedirected;
        _errorRedirected = Console.IsErrorRedirected;
    }

    internal void RenderFullResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(ConsoleRender.FormatNote("Received response is empty or contains whitespace only."));
        }
        else if (Console.IsOutputRedirected)
        {
            Console.WriteLine(response);
        }
        else
        {
            // Render the markdown only if standard output is not redirected.
            string text = _markdownRender.RenderText(response);
            if (!Utils.LeadingWhiteSpaceHasNewLine(text))
            {
                Console.WriteLine();
            }

            Console.WriteLine(text);
        }
    }

    internal async Task<T> RunWithSpinnerAsync<T>(Func<Task<T>> func, string status = null)
    {
        if (_outputRedirected && _errorRedirected)
        {
            // Since both stdout and stderr are redirected, no need to use a spinner for the async call.
            return await func().ConfigureAwait(false);
        }

        using var _ = _outputRedirected ? ConsoleRender.UseErrorConsole() : null;
        Capabilities caps = AnsiConsole.Profile.Capabilities;
        bool interactive = caps.Interactive;

        try
        {
            // When standard input is redirected, AnsiConsole's auto detection believes it's non-interactive,
            // and thus doesn't render Status or Progress. However, redirected input should not affect the
            // Status/Progress rendering as long as its output target, stderr or stdout, is not redirected.
            caps.Interactive = true;
            status ??= "Generating...";

            return await AnsiConsole
                .Status()
                .AutoRefresh(true)
                .Spinner(AsciiLetterSpinner.Default)
                .SpinnerStyle(new Style(Color.Olive))
                .StartAsync(
                    $"[italic slowblink]{status.EscapeMarkup()}[/]",
                    statusContext => func())
                .ConfigureAwait(false);
        }
        finally
        {
            caps.Interactive = interactive;
        }
    }

    internal T PromptForSelection<T>(string title, IEnumerable<T> choices, Func<T, string> converter = null)
    {
        if (choices is null || choices.Length is 0)
        {
            throw new ArgumentException("No choice was specified.", nameof(choices));
        }

        title ??= "Please select from the below list:";
        converter ??= static t => t is string str ? str : t.ToString();

        return AnsiConsole.Prompt(
            new SelectionPrompt<T>()
                .Title(title)
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to see more choices)[/]")
                .AddChoices(choices));
    }
}
