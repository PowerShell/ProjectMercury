using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Markdig;
using Markdown.VT;
using Spectre.Console;

namespace ShellCopilot.Kernel;

internal class CodeBlockVisitor : IVTRenderVisitor
{
    internal CodeBlockVisitor()
    {
        CodeBlock = new List<string>();
    }

    internal List<string> CodeBlock { get; }

    internal void Reset()
    {
        CodeBlock.Clear();
    }

    public void VisitCodeBlock(string code)
    {
        CodeBlock.Add(code);
    }
}

internal class MarkdownRender
{
    private readonly VTRenderer _vtRender;
    private readonly MarkdownPipeline _pipeline;
    private readonly StringWriter _stringWriter;
    private readonly CodeBlockVisitor _visitor;

    internal MarkdownRender()
    {
        _stringWriter = new StringWriter();
        _visitor = new CodeBlockVisitor();
        _vtRender = new VTRenderer(_stringWriter, new PSMarkdownOptionInfo(), _visitor);
        _vtRender.PushIndent("  ");
        _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    }

    internal List<string> GetAllCodeBlocks()
    {
        var code = _visitor.CodeBlock;
        return code.Count > 0 ? code : null;
    }

    internal string GetLastCodeBlock()
    {
        var code = _visitor.CodeBlock;
        return code.Count > 0 ? code[^1] : null;
    }

    internal string RenderText(string text)
    {
        try
        {
            // Reset the visitor before rendering a new markdown text.
            _visitor.Reset();
            return Markdig.Markdown.Convert(text, _vtRender, _pipeline).ToString();
        }
        finally
        {
            // Clear the 'StringWriter' so that the next rendering can start fresh.
            _stringWriter.GetStringBuilder().Clear();
        }
    }
}

internal readonly struct RenderElement<T>
{
    internal RenderElement(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        PropertyName = propertyName;
        PropertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        if (PropertyInfo is null || !PropertyInfo.CanRead)
        {
            throw new ArgumentException($"'{propertyName}' is not a public instance property or it's write-only.", nameof(propertyName));
        }

        CustomLabel = null;
        CustomValue = null;
    }

    internal RenderElement(string label, Func<T, string> valueFunc)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(valueFunc);

        CustomLabel = label;
        CustomValue = valueFunc;

        PropertyName = null;
        PropertyInfo = null;
    }

    internal string PropertyName { get; init; }
    internal PropertyInfo PropertyInfo { get; init; }

    internal string CustomLabel { get; init; }
    internal Func<T, string> CustomValue { get; init; }
}

internal static class ConsoleRender
{
    private static readonly IAnsiConsole s_errConsole = AnsiConsole.Create(
        new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
        }
    );

    internal static void RenderTable<T>(IList<T> sources, IList<RenderElement<T>> elements)
    {
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
            string header = element.PropertyName is null
                ? element.CustomLabel
                : element.PropertyName;
            spectreTable.AddColumn($"[green bold]{header}[/]");
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
                string value = element.PropertyName is null
                    ? element.CustomValue(source)
                    : element.PropertyInfo.GetValue(source)?.ToString();

                value ??= string.Empty;
                spectreTable.Rows.Update(rowIndex, i, new Markup(value));
            }
        }

        AnsiConsole.Write(spectreTable);
    }

    internal static void RenderList<T>(T source, IList<RenderElement<T>> elements)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(elements);

        if (elements.Count is 0)
        {
            return;
        }

        int maxLabelLen = 0;
        foreach (var element in elements)
        {
            int len = element.PropertyName is null
                ? element.CustomLabel.Length
                : element.PropertyName.Length;

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
            string col1, col2;
            if (element.PropertyName is null)
            {
                col1 = element.CustomLabel;
                col2 = element.CustomValue(source) ?? string.Empty;
            }
            else
            {
                col1 = element.PropertyName;
                col2 = element.PropertyInfo.GetValue(source)?.ToString() ?? string.Empty;
            }

            spectreTable.AddRow(Markup.FromInterpolated($"[green bold]{col1} :[/]"), new Markup(col2));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(spectreTable);
        AnsiConsole.WriteLine();
    }

    internal static string FormatInlineCode(string code)
    {
        return $"[indianred1 on grey19] {code} [/]";
    }

    internal static string FormatError(string message, bool usePrefix = true)
    {
        string prefix = usePrefix ? "ERROR: " : string.Empty;
        return $"[bold red]{prefix}{message}[/]";
    }

    internal static string FormatWarning(string message, bool usePrefix = true)
    {
        string prefix = usePrefix ? "WARNING: " : string.Empty;
        return $"[bold yellow]{prefix}{message}[/]";
    }

    internal static string FormatNote(string message)
    {
        return $"[orange3]NOTE:[/] {message}";
    }

    internal static async Task<string> AskForSecret(string prompt, CancellationToken cancellationToken)
    {
        return await new TextPrompt<string>(prompt)
            .PromptStyle(Color.Red)
            .Secret()
            .ShowAsync(AnsiConsole.Console, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<bool> AskForConfirmation(string prompt, CancellationToken cancellationToken, bool defaultValue = true)
    {
        return await new ConfirmationPrompt(prompt)
        {
            DefaultValue = defaultValue,
        }
        .ShowAsync(AnsiConsole.Console, cancellationToken)
        .ConfigureAwait(false);
    }

    internal static Disposable UseErrorConsole()
    {
        IAnsiConsole originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = s_errConsole;
        return new Disposable(() => AnsiConsole.Console = originalConsole);
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

internal sealed class Disposable : IDisposable
{
    private Action m_onDispose;

    internal static readonly Disposable NonOp = new Disposable();

    private Disposable()
    {
        m_onDispose = null;
    }

    public Disposable(Action onDispose)
    {
        m_onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    public void Dispose()
    {
        if (m_onDispose != null)
        {
            m_onDispose();
            m_onDispose = null;
        }
    }
}
