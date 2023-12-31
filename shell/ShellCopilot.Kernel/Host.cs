
using Azure.AI.OpenAI;
using ShellCopilot.Abstraction;
using Spectre.Console;

namespace ShellCopilot.Kernel;

/// <summary>
/// Host implementation of the Shell Copilot.
/// </summary>
internal sealed class Host : IHost
{
    private readonly bool _inputRedirected;
    private readonly bool _outputRedirected;
    private readonly bool _errorRedirected;
    private readonly IAnsiConsole _stderrConsole;

    internal MarkdownRender MarkdownRender { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="Host"/>.
    /// </summary>
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

    /// <inheritdoc/>
    public IHost Write(string value)
    {
        Console.Write(value);
        return this;
    }

    /// <inheritdoc/>
    public IHost WriteLine()
    {
        Console.WriteLine();
        return this;
    }

    /// <inheritdoc/>
    public IHost WriteLine(string value)
    {
        Console.WriteLine(value);
        return this;
    }

    /// <inheritdoc/>
    public IHost WriteErrorLine()
    {
        Console.Error.WriteLine();
        return this;
    }

    /// <inheritdoc/>
    public IHost WriteErrorLine(string value)
    {
        Console.Error.WriteLine(value);
        return this;
    }

    /// <inheritdoc/>
    public IHost Markup(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            AnsiConsole.Markup(value);
        }

        return this;
    }

    /// <inheritdoc/>
    public IHost MarkupLine(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Console.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine(value);
        }

        return this;
    }

    /// <inheritdoc/>
    public IHost MarkupErrorLine(string value)
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

    /// <inheritdoc/>
    public IHost MarkupNoteLine(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Console.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine($"[orange3]NOTE:[/] {value}");
        }

        return this;
    }

    /// <inheritdoc/>
    public IHost MarkupWarningLine(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Console.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold yellow]WARNING: {value}[/]");
        }

        return this;
    }

    /// <inheritdoc/>
    public IStreamRender NewStreamRender(CancellationToken cancellationToken)
    {
        return _outputRedirected
            ? new DummyStreamRender(cancellationToken)
            : new FancyStreamRender(MarkdownRender, cancellationToken);
    }

    /// <inheritdoc/>
    public void RenderFullResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            WriteLine();
            MarkupLine(ConsoleRender.FormatNote("Received response is empty or contains whitespace only."));
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

    /// <inheritdoc/>
    public async Task<string> RenderStreamingResponse(StreamingChatCompletions response, CancellationToken cancellationToken)
    {
        var streamingRender = new FancyStreamRender(MarkdownRender, cancellationToken);

        try
        {
            // Hide the cursor position when rendering the streaming response.
            Console.CursorVisible = false;

            // Cannot pass in `cancellationToken` to `GetChoicesStreaming()` and `GetMessageStreaming()` methods.
            // Doing so will result in an exception in Azure.OpenAI when we are cancelling the operation.
            // TODO: Use the latest preview version. The bug may have been fixed.
            await foreach (StreamingChatChoice choice in response.GetChoicesStreaming())
            {
                await foreach (ChatMessage message in choice.GetMessageStreaming())
                {
                    if (string.IsNullOrEmpty(message.Content))
                    {
                        continue;
                    }

                    streamingRender.Refresh(message.Content);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore the cancellation exception.
        }
        finally
        {
            Console.CursorVisible = true;
        }

        Console.WriteLine();
        return streamingRender.AccumulatedContent;
    }

    /// <inheritdoc/>
    public void RenderTable<T>(IList<T> sources, IList<IRenderElement<T>> elements)
    {
        RequireStdoutOrStderr(operation: "render table");

        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(elements);

        if (sources.Count is 0 || elements.Count is 0)
        {
            return;
        }

        var spectreTable = new Table()
            .LeftAligned()
            .SimpleBorder()
            .BorderColor(Color.Green);

        // Add columns.
        foreach (var element in elements)
        {
            spectreTable.AddColumn($"[green bold]{element.Name}[/]");
        }

        // Add rows.
        int rowIndex = -1;
        foreach (T source in sources)
        {
            spectreTable.AddEmptyRow();
            rowIndex++;

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                string value = element.Value(source) ?? string.Empty;
                spectreTable.Rows.Update(rowIndex, i, new Markup(value));
            }
        }

        AnsiConsole.Write(spectreTable);
    }

    /// <inheritdoc/>
    public void RenderList<T>(T source, IList<IRenderElement<T>> elements)
    {
        RequireStdoutOrStderr(operation: "render list");

        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(elements);

        if (elements.Count is 0)
        {
            return;
        }

        int maxLabelLen = 0;
        foreach (var element in elements)
        {
            int len = element.Name.Length;
            if (len > maxLabelLen)
            {
                maxLabelLen = len;
            }
        }

        var spectreTable = new Table()
            .HideHeaders()
            .NoBorder()
            .AddColumn("Labels", c => c.NoWrap().LeftAligned().Width(maxLabelLen + 3))
            .AddColumn("Values", c => c.PadRight(0));

        foreach (var element in elements)
        {
            string col1 = element.Name;
            string col2 = element.Value(source) ?? string.Empty;
            spectreTable.AddRow(Spectre.Console.Markup.FromInterpolated($"[green bold]{col1} :[/]"), new Markup(col2));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(spectreTable);
        AnsiConsole.WriteLine();
    }

    /// <inheritdoc/>
    public async Task<T> RunWithSpinnerAsync<T>(Func<Task<T>> func, string status = null)
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

    /// <inheritdoc/>
    public async Task<T> PromptForSelectionAsync<T>(string title, IEnumerable<T> choices, Func<T, string> converter = null, CancellationToken cancellationToken = default)
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

    /// <inheritdoc/>
    public async Task<bool> PromptForConfirmationAsync(string prompt, bool defaultValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);
        string operation = "prompt for confirmation";

        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        IAnsiConsole ansiConsole = _outputRedirected ? _stderrConsole : AnsiConsole.Console;
        var confirmation = new ConfirmationPrompt(prompt) { DefaultValue = defaultValue };

        return await confirmation.ShowAsync(ansiConsole, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> PromptForSecretAsync(string prompt, CancellationToken cancellationToken)
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
