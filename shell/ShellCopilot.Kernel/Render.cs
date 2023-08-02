using Markdig;
using Markdown.VT;
using Spectre.Console;
using System.Reflection;

namespace ShellCopilot.Kernel;

internal class MarkdownRender
{
    private readonly VTRenderer _vtRender;
    private readonly MarkdownPipeline _pipeline;
    private readonly StringWriter _stringWriter;

    internal MarkdownRender()
    {
        _stringWriter = new StringWriter();
        _vtRender = new VTRenderer(_stringWriter, new PSMarkdownOptionInfo());
        _vtRender.PushIndent("  ");
        _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    }

    internal string RenderText(string text)
    {
        try
        {
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
}
