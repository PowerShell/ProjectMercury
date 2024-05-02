using System.Reflection;
using System.Text;
using Markdig.Helpers;
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
        if (Console.IsErrorRedirected || string.IsNullOrEmpty(value))
        {
            Console.Error.WriteLine(value);
        }
        else
        {
            _stderrConsole.MarkupLine(Formatter.Error(value.EscapeMarkup()));
        }

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
            AnsiConsole.MarkupLine(Formatter.Warning(value));
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
            MarkupNoteLine("Received response is empty or contains whitespace only.");
        }
        else if (_outputRedirected)
        {
            WriteLine(response);
        }
        else
        {
            // Render the markdown only if standard output is not redirected.
            string text = MarkdownRender.RenderText(response);
            if (!LeadingWhiteSpaceHasNewLine(text))
            {
                WriteLine();
            }

            WriteLine(text);
        }
    }

    /// <inheritdoc/>
    public void RenderTable<T>(IList<T> sources)
    {
        RequireStdoutOrStderr(operation: "render table");
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count is 0)
        {
            return;
        }

        var elements = new List<IRenderElement<T>>();
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead)
            {
                elements.Add(new PropertyElement<T>(property));
            }
        }

        RenderTable(sources, elements);
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
    public void RenderList<T>(T source)
    {
        RequireStdoutOrStderr(operation: "render list");
        ArgumentNullException.ThrowIfNull(source);

        if (source is IDictionary<string, string> dict)
        {
            var elements = new List<IRenderElement<IDictionary<string, string>>>(capacity: dict.Count);
            foreach (string key in dict.Keys)
            {
                elements.Add(new KeyValueElement<IDictionary<string, string>>(key));
            }

            RenderList(dict, elements);
        }
        else
        {
            var elements = new List<IRenderElement<T>>();
            foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead)
                {
                    elements.Add(new PropertyElement<T>(property));
                }
            }

            RenderList(source, elements);
        }
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
            .AddColumn("Labels", c => c.NoWrap().LeftAligned().Width(maxLabelLen + 4))
            .AddColumn("Values");

        foreach (var element in elements)
        {
            string col1 = element.Name;
            string col2 = element.Value(source) ?? string.Empty;
            spectreTable.AddRow(Spectre.Console.Markup.FromInterpolated($"  [green bold]{col1} :[/]"), new Markup(col2));
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
    public async Task<T> RunWithSpinnerAsync<T>(Func<IStatusContext, Task<T>> func, string status)
    {
        if (_outputRedirected && _errorRedirected)
        {
            // Since both stdout and stderr are redirected, no need to use a spinner for the async call.
            return await func(null).ConfigureAwait(false);
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
                    ctx => func(new StatusContext(ctx)))
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

        prompt = $"[orange3 on italic]{(prompt.Contains("[/]") ? prompt : prompt.EscapeMarkup())}[/]";
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

    /// <inheritdoc/>
    public async Task<string> PromptForTextAsync(string prompt, bool optional, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);
        string operation = "prompt for text";

        RequireStdin(operation);
        RequireStdoutOrStderr(operation);

        IAnsiConsole ansiConsole = _outputRedirected ? _stderrConsole : AnsiConsole.Console;
        string promptToUse = optional ? $"[grey][[Optional]][/] {prompt}" : prompt;
        return await new TextPrompt<string>(promptToUse) { AllowEmpty = optional }
            .PromptStyle(new Style(Color.Teal))
            .ShowAsync(ansiConsole, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Render text content in the "for-reference" style.
    /// </summary>
    /// <param name="header">Title for the content.</param>
    /// <param name="content">Text to be rendered.</param>
    internal void RenderReferenceText(string header, string content)
    {
        RequireStdoutOrStderr(operation: "Render reference");

        var panel = new Panel($"\n[italic]{content.EscapeMarkup()}[/]\n")
            .RoundedBorder()
            .BorderColor(Color.DarkCyan)
            .Header($"[orange3 on italic] {header.Trim()} [/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
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

    /// <summary>
    /// Check if the leading whitespace characters of <paramref name="text"/> contains a newline.
    /// </summary>
    private static bool LeadingWhiteSpaceHasNewLine(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                return true;
            }

            if (!c.IsWhitespace())
            {
                break;
            }
        }

        return false;
    }
}

/// <summary>
/// Wrapper of the <see cref="Spectre.Console.StatusContext"/> to not expose 'Spectre.Console' types,
/// so as to avoid requiring all agent implementations to depend on the 'Spectre.Console' package.
/// </summary>
internal sealed class StatusContext : IStatusContext
{
    private readonly Spectre.Console.StatusContext _context;

    internal StatusContext(Spectre.Console.StatusContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public void Status(string status)
    {
        _context.Status($"[italic slowblink]{status.EscapeMarkup()}[/]");
    }
}

internal sealed class AsciiLetterSpinner : Spinner
{
    private const int FrameNumber = 8;
    private readonly List<string> _frames;

    internal static readonly AsciiLetterSpinner Default = new();

    internal AsciiLetterSpinner(int prefixGap = 0, int charLength = 12)
    {
        _frames = new List<string>(capacity: FrameNumber);
        StringBuilder sb = new(capacity: prefixGap + charLength + 2);

        var gap = prefixGap is 0 ? null : new string(' ', prefixGap);
        for (var i = 0; i < FrameNumber; i++)
        {
            sb.Append(gap).Append('/');
            for (var j = 0; j < charLength; j++)
            {
                sb.Append((char)Random.Shared.Next(33, 127));
            }

            _frames.Add(sb.Append('/').ToString());
            sb.Clear();
        }
    }

    public override TimeSpan Interval => TimeSpan.FromMilliseconds(100);
    public override bool IsUnicode => false;
    public override IReadOnlyList<string> Frames => _frames;
}

internal static class Formatter
{
    internal static string InlineCode(string code)
    {
        return $"[indianred1 on grey19] {code} [/]";
    }

    internal static string Error(string message)
    {
        return $"[bold red]ERROR: {message}[/]";
    }

    internal static string Warning(string message)
    {
        return $"[bold yellow]WARNING: {message}[/]";
    }
}
