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
    private readonly string _plainTextForeground;
    private readonly string _plainTextBackground;

    public CodeBlockRenderer()
    {
        var styles = StyleDictionary.DefaultDark;
        if (styles.TryGetValue(ScopeName.PlainText, out Style style))
        {
            _plainTextForeground = style.Foreground.ToVTColor();
            _plainTextBackground = style.Background.ToVTColor(isForeground: false);
        }
        _vtHighlighter = new VTSyntaxHighlighter(styles);
    }

    protected override void Write(VTRenderer renderer, CodeBlock obj)
    {
        renderer.WriteLine($"{_plainTextForeground}{_plainTextBackground}");
        renderer.PushIndent("  ");

        ILanguage language = null;
        if (obj is FencedCodeBlock fencedCodeBlock && fencedCodeBlock.Info is string info)
        {
            string infoPrefix = (obj.Parser as FencedCodeBlockParser)?.InfoPrefix ?? FencedCodeBlockParser.DefaultInfoPrefix;
            string langId = info.StartsWith(infoPrefix, StringComparison.Ordinal) ? info.Substring(infoPrefix.Length) : info;

            language = _vtHighlighter.FindLanguageById(langId);
        }

        if (language is null)
        {
            renderer.WriteLeafRawLines(obj);
        }
        else
        {
            string code = ExtractCode(obj);
            string vtText = _vtHighlighter.GetVTString(code, language);

            int start = 0;
            while (true)
            {
                if (start == vtText.Length)
                {
                    break;
                }

                int nlIndex = vtText.IndexOf('\n', start);
                int length = nlIndex is -1 ? vtText.Length - start : nlIndex - start + 1;
                var span = vtText.AsSpan(start, length);

                // Call 'WriteLine' explicitly to make sure the indentation is applied.
                renderer.Write(span.Trim('\n'));
                renderer.WriteLine();

                if (nlIndex is -1)
                {
                    break;
                }

                start = nlIndex + 1;
            }
        }

        renderer.PopIndent();
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
