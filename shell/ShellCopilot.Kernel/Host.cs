
using SharpToken;
using Spectre.Console;

namespace ShellCopilot.Kernel;

internal class Host
{
    private readonly bool _inputRedirected;
    private readonly bool _outputRedirected;
    private readonly bool _errorRedirected;

    internal MarkdownRender MarkdownRender { get; }

    internal Host()
    {
        _inputRedirected = Console.IsInputRedirected;
        _outputRedirected = Console.IsOutputRedirected;
        _errorRedirected = Console.IsErrorRedirected;

        MarkdownRender = new MarkdownRender();
    }

    /// <summary>
    /// Write out the string literally to standard output.
    /// </summary>
    internal Host WriteLine(string value = null)
    {
        Console.WriteLine(value);
        return this;
    }

    /// <summary>
    /// Write out the string literally to standard error.
    /// </summary>
    internal Host WriteErrorLine(string value = null)
    {
        Console.Error.WriteLine(value);
        return this;
    }

    /// <summary>
    /// Write out the markup string to standard output.
    /// </summary>
    internal Host WriteMarkupLine(string value)
    {
        AnsiConsole.MarkupLine(value);
        return this;
    }

    /// <summary>
    /// Write out the markup string to standard error.
    /// </summary>
    internal Host WriteErrorMarkupLine(string value)
    {
        using var _ = ConsoleRender.UseErrorConsole();
        AnsiConsole.MarkupLine($"[bold red]ERROR: {value}[/]");
        return this;
    }

    internal StreamRender NewStreamRender(CancellationToken cancellationToken)
    {
        return new StreamRender(MarkdownRender, cancellationToken);
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
            string text = MarkdownRender.RenderText(response);
            if (!Utils.LeadingWhiteSpaceHasNewLine(text))
            {
                Console.WriteLine();
            }

            Console.WriteLine(text);
        }
    }

    internal async Task RunWithSpinnerAsync(Func<Task> func, string status = null)
    {
        if (_outputRedirected && _errorRedirected)
        {
            // Since both stdout and stderr are redirected, no need to use a spinner for the async call.
            await func().ConfigureAwait(false);
            return;
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

            await AnsiConsole
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
        string operation = "prompt for selection";
        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        if (choices is null || !choices.Any())
        {
            throw new ArgumentException("No choice was specified.", nameof(choices));
        }

        using var _ = _outputRedirected ? ConsoleRender.UseErrorConsole() : null;
        title ??= "Please select from the below list:";
        converter ??= static t => t is string str ? str : t.ToString();

        return AnsiConsole.Prompt(
            new SelectionPrompt<T>()
                .Title(title)
                .PageSize(10)
                .UseConverter(converter)
                .MoreChoicesText("[grey](Move up and down to see more choices)[/]")
                .AddChoices(choices));
    }

    internal bool PromptForConfirmation(string prompt, bool defaultValue)
    {
        string operation = "prompt for confirmation";
        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentException("A prompt string is required.", nameof(prompt));
        }

        using var _ = _outputRedirected ? ConsoleRender.UseErrorConsole() : null;
        return AnsiConsole.Confirm(prompt, defaultValue);
    }

    internal async Task<T> PromptForSelectionAsync<T>(string title, IEnumerable<T> choices, CancellationToken cancellationToken, Func<T, string> converter = null)
    {
        string operation = "prompt for selection";
        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        if (choices is null || !choices.Any())
        {
            throw new ArgumentException("No choice was specified.", nameof(choices));
        }

        using var _ = _outputRedirected ? ConsoleRender.UseErrorConsole() : null;
        title ??= "Please select from the below list:";
        converter ??= static t => t is string str ? str : t.ToString();

        var selection = new SelectionPrompt<T>()
            .Title(title)
            .PageSize(10)
            .UseConverter(converter)
            .MoreChoicesText("[grey](Move up and down to see more choices)[/]")
            .AddChoices(choices);

        return await selection.ShowAsync(AnsiConsole.Console, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool> PromptForConfirmationAsync(string prompt, CancellationToken cancellationToken, bool defaultValue = true)
    {
        string operation = "prompt for confirmation";
        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentException("A prompt string is required.", nameof(prompt));
        }

        using var _ = _outputRedirected ? ConsoleRender.UseErrorConsole() : null;
        var confirmation = new ConfirmationPrompt(prompt)
        {
            DefaultValue = defaultValue
        };

        return await confirmation.ShowAsync(AnsiConsole.Console, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<string> PromptForSecret(string prompt, CancellationToken cancellationToken)
    {
        string operation = "prompt for secret";
        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentException("A prompt string is required.", nameof(prompt));
        }

        return await new TextPrompt<string>(prompt)
            .PromptStyle(Color.Red)
            .Secret()
            .ShowAsync(AnsiConsole.Console, cancellationToken)
            .ConfigureAwait(false);
    }

    private void RequireStdin(string operation)
    {
        if (_inputRedirected)
        {
            throw new InvalidOperationException($"Cannot {operation} when stdin is redirected.");
        }
    }

    private void RequireStdoutOrStderr(string operation)
    {
        if (_outputRedirected && _errorRedirected)
        {
            throw new InvalidOperationException($"Cannot {operation} when both the stdout and stderr are redirected.");
        }
    }
}
