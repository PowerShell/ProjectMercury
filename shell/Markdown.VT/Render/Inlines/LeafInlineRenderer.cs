// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax.Inlines;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for leaf elements like plain text in paragraphs.
/// </summary>
internal class LeafInlineRenderer : VTObjectRenderer<LeafInline>
{
    protected override void Write(VTRenderer renderer, LeafInline obj)
    {
        // If the next sibling is null, then this is the last line in the paragraph.
        // Add new line character at the end.
        // Else just write without newline at the end.
        if (obj.NextSibling == null)
        {
            renderer.WriteLine(obj.ToString());
        }
        else
        {
            renderer.Write(obj.ToString());
        }
    }
}
