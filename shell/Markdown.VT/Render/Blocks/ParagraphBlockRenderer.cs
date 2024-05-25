// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for paragraphs.
/// </summary>
internal class ParagraphBlockRenderer : VTObjectRenderer<ParagraphBlock>
{
    protected override void Write(VTRenderer renderer, ParagraphBlock obj)
    {
        if (obj.Parent is MarkdownDocument)
        {
            // Write an empty line if it's a top-level paragraph.
            renderer.WriteLine();
        }
        else if (obj.Parent is ListItemBlock listItem)
        {
            // When the current paragraph is in the middle of a 'ListItemBlock'
            // that contains multiple items, then we write an empty line if the
            // previous item is not a paragraph, e.g. when the previous item is
            // a 'CodeBlock'.
            int i = 0;
            for (; i < listItem.Count; i++)
            {
                if (listItem[i] == obj)
                {
                    break;
                }
            }

            if (i > 0 && listItem[i - 1] is not ParagraphBlock)
            {
                renderer.WriteLine();
            }
        }

        if (obj.Column > 0)
        {
            renderer.PushIndentAndUpdateWidth(new string(' ', obj.Column));
        }

        renderer.WriteLeafInline(obj);
        renderer.EnsureLine();

        if (obj.Column > 0)
        {
            renderer.PopIndentAndUpdateWidth();
        }
    }
}
