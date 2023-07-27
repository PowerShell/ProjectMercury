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
            renderer.WriteLine();
        }

        renderer.WriteLeafInline(obj);
        renderer.EnsureLine();
    }
}
