// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

using ColorCode;
using ColorCode.Common;
using ColorCode.Styling;
using ColorCode.VT;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace Markdown.VT;

public class CodeBlockRenderer : VTObjectRenderer<CodeBlock>
{
    private readonly VTSyntaxHighlighter _vtHighlighter;
    private readonly string _plainFgBgColors;

    public CodeBlockRenderer()
    {
        var styles = StyleDictionary.DefaultDark;
        if (styles.TryGetValue(ScopeName.PlainText, out Style style))
        {
            string foreground = style.Foreground.ToVTColor();
            string background = style.Background.ToVTColor(isForeground: false);
            _plainFgBgColors = $"{foreground}{background}";
        }
        _vtHighlighter = new VTSyntaxHighlighter(styles);
    }

    protected override void Write(VTRenderer renderer, CodeBlock obj)
    {
        renderer.WriteLine();
        renderer.PushIndentAndUpdateWidth(VTRenderer.DefaultIndent);

        ILanguage language = null;
        if (obj is FencedCodeBlock fencedCodeBlock && fencedCodeBlock.Info is string info)
        {
            string infoPrefix = (obj.Parser as FencedCodeBlockParser)?.InfoPrefix ?? FencedCodeBlockParser.DefaultInfoPrefix;
            string langId = info.StartsWith(infoPrefix, StringComparison.Ordinal) ? info.Substring(infoPrefix.Length) : info;
            language = string.IsNullOrEmpty(langId) ? null : Languages.FindById(langId);
        }

        // Call the visitor with the original code.
        string code = ExtractCode(obj);
        renderer.Visitor?.VisitCodeBlock(code);

        int start = 0;
        string vtText = _vtHighlighter.GetVTString(code, language);

        while (true)
        {
            if (start == vtText.Length)
            {
                break;
            }

            int nlIndex = vtText.IndexOf('\n', start);
            int length = nlIndex is -1 ? vtText.Length - start : nlIndex - start + 1;
            var span = vtText.AsSpan(start, length);

            // We will write out indents before writing out lines of the decorated code blocks.
            // If the line starts with the default foreground and background colors, then we
            // move the color sequences up to before the indents.
            if (span.StartsWith(_plainFgBgColors, StringComparison.Ordinal))
            {
                renderer.Writer.Write(_plainFgBgColors);
                span = span[_plainFgBgColors.Length..];
            }

            // Call 'WriteLine' explicitly to make sure the indentation is applied.
            // This is sort of an implementation detail: `render.Write` writes the indents only if the previous call
            // wrote out a newline. So we call `Render.WriteLine` explicitly to make `Render` know that a newline was
            // just written out.
            renderer.Write(span.Trim('\n'));
            renderer.WriteLine();

            if (nlIndex is -1)
            {
                break;
            }

            start = nlIndex + 1;
        }

        renderer.PopIndentAndUpdateWidth();
        renderer.EnsureLine();
    }

    private static string ExtractCode(LeafBlock leafBlock)
    {
        var code = new StringBuilder();
        var lines = leafBlock.Lines.Lines ?? Array.Empty<StringLine>();
        var totalLines = lines.Length;

        for (var index = 0; index < totalLines; index++)
        {
            var line = lines[index];
            var slice = line.Slice;

            if (slice.Text == null)
            {
                continue;
            }

            var lineText = slice.Text.Substring(slice.Start, slice.Length);

            if (index > 0)
            {
                code.AppendLine();
            }

            code.Append(lineText);
        }

        return code.ToString();
    }
}
