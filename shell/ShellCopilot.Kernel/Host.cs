
using SharpToken;
using ShellCopilot.Abstraction;
using Spectre.Console;

namespace ShellCopilot.Kernel;

internal class Host
{
    private readonly bool _inputRedirected;
    private readonly bool _outputRedirected;
    private readonly bool _errorRedirected;
    private readonly IAnsiConsole _stderrConsole;

    internal MarkdownRender MarkdownRender { get; }

    internal Host()
    {
        _inputRedirected = Console.IsInputRedirected;
        _outputRedirected = Console.IsOutputRedirected;
        _errorRedirected = Console.IsErrorRedirected;
        _stderrConsole = AnsiConsole.Create(
            new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Detect,
                ColorSystem = ColorSystemSupport.Detect,
                Out = new AnsiConsoleOutput(Console.Error),
            }
        );

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
        if (string.IsNullOrEmpty(value))
        {
            Console.Error.WriteLine();
        }
        else
        {
            _stderrConsole.MarkupLine($"[bold red]ERROR: {value}[/]");
        }

        return this;
    }

    /// <summary>
    /// Create a new instance of the <see cref="IStreamRender"/>.
    /// If the stdout is redirected, the returned render will simply write the raw chunks out.
    /// </summary>
    internal IStreamRender NewStreamRender(CancellationToken cancellationToken)
    {
        return _outputRedirected
            ? new DummyStreamRender(cancellationToken)
            : new FancyStreamRender(MarkdownRender, cancellationToken);
    }

    /// <summary>
    /// Render the response as markdown and write to the standard output.
    /// If the stdout is redirected, the raw response will be written out as is.
    /// </summary>
    internal void RenderFullResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            WriteLine();
            WriteMarkupLine(ConsoleRender.FormatNote("Received response is empty or contains whitespace only."));
        }
        else if (_outputRedirected)
        {
            WriteLine(response);
        }
        else
        {
            // Render the markdown only if standard output is not redirected.
            string text = MarkdownRender.RenderText(response);
            if (!Utils.LeadingWhiteSpaceHasNewLine(text))
            {
                WriteLine();
            }

            WriteLine(text);
        }
    }

    /// <summary>
    /// Run the given task <paramref name="func"/> while showing a spinner.
    /// </summary>
    internal async Task<T> RunWithSpinnerAsync<T>(Func<Task<T>> func, string status = null)
    {
        if (_outputRedirected && _errorRedirected)
        {
            // Since both stdout and stderr are redirected, no need to use a spinner for the async call.
            return await func().ConfigureAwait(false);
        }

        IAnsiConsole ansiConsole = _outputRedirected ? _stderrConsole : AnsiConsole.Console;
        Capabilities caps = ansiConsole.Profile.Capabilities;
        bool interactive = caps.Interactive;

        try
        {
            // When standard input is redirected, AnsiConsole's auto detection believes it's non-interactive,
            // and thus doesn't render Status or Progress. However, redirected input should not affect the
            // Status/Progress rendering as long as its output target, stderr or stdout, is not redirected.
            caps.Interactive = true;
            status ??= "Generating...";

            return await ansiConsole
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

    /// <summary>
    /// Prompt for selection asynchronously.
    /// </summary>
    internal async Task<T> PromptForSelectionAsync<T>(string title, IEnumerable<T> choices, CancellationToken cancellationToken, Func<T, string> converter = null)
    {
        string operation = "prompt for selection";
        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        if (choices is null || !choices.Any())
        {
            throw new ArgumentException("No choice was specified.", nameof(choices));
        }

        IAnsiConsole ansiConsole = _outputRedirected ? _stderrConsole : AnsiConsole.Console;
        title ??= "Please select from the below list:";
        converter ??= static t => t is string str ? str : t.ToString();

        var selection = new SelectionPrompt<T>()
            .Title(title)
            .PageSize(10)
            .UseConverter(converter)
            .MoreChoicesText("[grey](Move up and down to see more choices)[/]")
            .AddChoices(choices);

        return await selection.ShowAsync(ansiConsole, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Prompt for confirmation asynchronously.
    /// </summary>
    internal async Task<bool> PromptForConfirmationAsync(string prompt, CancellationToken cancellationToken, bool defaultValue = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);
        string operation = "prompt for confirmation";

        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        IAnsiConsole ansiConsole = _outputRedirected ? _stderrConsole : AnsiConsole.Console;
        var confirmation = new ConfirmationPrompt(prompt) { DefaultValue = defaultValue };

        return await confirmation.ShowAsync(ansiConsole, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Prompt for secret asynchronously.
    /// </summary>
    internal async Task<string> PromptForSecretAsync(string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);
        string operation = "prompt for secret";

        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        IAnsiConsole ansiConsole = _outputRedirected ? _stderrConsole : AnsiConsole.Console;
        return await new TextPrompt<string>(prompt)
            .PromptStyle(Color.Red)
            .Secret()
            .ShowAsync(ansiConsole, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Throw exception if standard input is redirected.
    /// </summary>
    /// <param name="operation">The intended operation.</param>
    /// <exception cref="InvalidOperationException">Throw the exception if stdin is redirected.</exception>
    private void RequireStdin(string operation)
    {
        if (_inputRedirected)
        {
            throw new InvalidOperationException($"Cannot {operation} when stdin is redirected.");
        }
    }

    /// <summary>
    /// Throw exception if both standard output and error are redirected.
    /// </summary>
    /// <param name="operation">The intended operation.</param>
    /// <exception cref="InvalidOperationException">Throw the exception if stdout and stderr are both redirected.</exception>
    private void RequireStdoutOrStderr(string operation)
    {
        if (_outputRedirected && _errorRedirected)
        {
            throw new InvalidOperationException($"Cannot {operation} when both the stdout and stderr are redirected.");
        }
    }
}
