// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax.Inlines;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for line breaks.
/// </summary>
internal class LineBreakInlineRenderer : VTObjectRenderer<LineBreakInline>
{
    protected override void Write(VTRenderer renderer, LineBreakInline obj)
    {
        if (renderer.IsLastInContainer)
        {
            return;
        }

        // Treat soft break as hard break in terminal, for better readability.
        renderer.WriteLine();
    }
}
