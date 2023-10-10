using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

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

internal struct Point
{
    public int X;
    public int Y;

    internal Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override readonly string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0},{1}", X, Y);
    }
}

internal partial class StreamingRender
{
    internal const char ESC = '\x1b';
    internal static readonly Regex AnsiRegex = CreateAnsiRegex();

    private string _currentText;
    private int _bufferWidth, _bufferHeight;
    private Point _initialCursor;

    internal StreamingRender()
    {
        _currentText = string.Empty;
        _bufferWidth = Console.BufferWidth;
        _bufferHeight = Console.BufferHeight;
        _initialCursor = new(Console.CursorLeft, Console.CursorTop);
    }

    internal void Refresh(string newText)
    {
        if (string.Equals(newText, _currentText, StringComparison.Ordinal))
        {
            return;
        }

        int newTextStartIndex = 0;
        var cursorStart = _initialCursor;
        bool redoWholeLine = false;

        if (!string.IsNullOrEmpty(_currentText))
        {
            int index = SameUpTo(newText, out redoWholeLine);
            newTextStartIndex = index + 1;

            // When the new text start exactly with the current text, we just continue to write.
            // No need to move the cursor in that case.
            bool moveCursor = index < _currentText.Length - 1;
            if (moveCursor && index >= 0)
            {
                // When 'index == -1', we just move the cursor to the initial position.
                // Otherwise, calculate the cursor position for the next write.
                string oldPlainText = GetPlainText(_currentText[..newTextStartIndex]);
                cursorStart = ConvertOffsetToPoint(cursorStart, oldPlainText, oldPlainText.Length);
            }

            if (moveCursor)
            {
                Console.SetCursorPosition(cursorStart.X, cursorStart.Y);
                // erase from that cursor position (inclusive) to the end of the display
                Console.Write("\x1b[0J");
            }
            else
            {
                cursorStart = new(Console.CursorLeft, Console.CursorTop);
            }
        }

        Console.Out.Write(newText.AsSpan(newTextStartIndex));

        // Update the streaming render
        int topMax = _bufferHeight - 1;
        if (Console.CursorTop == topMax)
        {
            // If the current cursor top is less than top-max, then there was no scrolling-up and the
            // initial cursor position was not changed.
            // But if it's equal to top-max, then the terminal buffer may have scrolled, and in that
            // case we need to re-calculate and update the relative position of the initial cursor.
            string newPlainText = GetPlainText(newText[newTextStartIndex..]);
            Point cursorEnd = ConvertOffsetToPoint(cursorStart, newPlainText, newPlainText.Length);

            if (cursorEnd.Y > topMax)
            {
                int offset = cursorEnd.Y - topMax;
                _initialCursor.Y -= offset;
            }
        }

        _currentText = newText;

        // Wait for a short interval before refreshing again for the in-coming payload.
        // We use a smaller interval (20ms) when rendering code blocks, so as to reduce the flashing when
        // rewriting the whole line. Otherwise, we use the 50ms interval.
        Thread.Sleep(redoWholeLine ? 20 : 50);
    }

    /// <summary>
    /// The regular expression for matching ANSI escape sequences, which consists of the followings in the same order:
    ///  - graphics regex: graphics/color mode ESC[1;2;...m
    ///  - csi regex: CSI escape sequences
    ///  - hyperlink regex: hyperlink escape sequences. Note: '.*?' makes '.*' do non-greedy match.
    /// </summary>
    [GeneratedRegex(@"(\x1b\[\d+(;\d+)*m)|(\x1b\[\?\d+[hl])|(\x1b\]8;;.*?\x1b\\)", RegexOptions.Compiled)]
    private static partial Regex CreateAnsiRegex();

    private string GetPlainText(string text)
    {
        if (!text.Contains(ESC))
        {
            return text;
        }

        return AnsiRegex.Replace(text, string.Empty);
    }

    /// <summary>
    /// Return the index up to which inclusively we consider the current text and the new text are the same.
    /// Note that, the return value can range from -1 (nothing is the same) to `cur_text.Length - 1` (all is the same).
    /// </summary>
    private int SameUpTo(string newText, out bool redoWholeLine)
    {
        int i = 0;
        for (; i < _currentText.Length; i++)
        {
            if (_currentText[i] != newText[i])
            {
                break;
            }
        }

        int j = i - 1;
        redoWholeLine = false;

        if (i < _currentText.Length && _currentText.IndexOf("\x1b[0m", i, StringComparison.Ordinal) != -1)
        {
            // When the portion to be re-written contains the 'RESET' sequence, it's safer to re-write the whole
            // logical line because all existing color or font effect was already reset and so those decorations
            // would be lost if we re-write from the middle of the logical line.
            // Well, this assumes decorations always start fresh for a new logical line, which is truely the case
            // for the code block syntax highlighting done by our Markdown VT render.
            redoWholeLine = true;
            for (; j >= 0; j--)
            {
                if (_currentText[j] == '\n')
                {
                    break;
                }
            }
        }

        return j;
    }

    private Point ConvertOffsetToPoint(Point point, string text, int offset)
    {
        int x = point.X;
        int y = point.Y;

        for (int i = 0; i < offset; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                y += 1;
                x = 0;
            }
            else
            {
                int size = c.GetCellWidth();
                x += size;
                // Wrap?  No prompt when wrapping
                if (x >= _bufferWidth)
                {
                    // If character didn't fit on current line, it will move entirely to the next line.
                    x = (x == _bufferWidth) ? 0 : size;

                    // If cursor is at column 0 and the next character is newline, let the next loop
                    // iteration increment y.
                    if (x != 0 || !(i + 1 < offset && text[i + 1] == '\n'))
                    {
                        y += 1;
                    }
                }
            }
        }

        // If next character actually exists, and isn't newline, check if wider than the space left on the current line.
        if (text.Length > offset && text[offset] != '\n')
        {
            int size = text[offset].GetCellWidth();
            if (x + size > _bufferWidth)
            {
                // Character was wider than remaining space, so character, and cursor, appear on next line.
                x = 0;
                y++;
            }
        }

        return new Point(x, y);
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
