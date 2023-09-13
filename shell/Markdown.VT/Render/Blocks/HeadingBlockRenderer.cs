// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for headings.
/// </summary>
internal class HeadingBlockRenderer : VTObjectRenderer<HeadingBlock>
{
    private const string VTReset = "\x1b[0m";
    private const string VTStyle = "\x1b[38;5;208m";

    protected override void Write(VTRenderer renderer, HeadingBlock obj)
    {
        if (obj.Parent is MarkdownDocument)
        {
            renderer.WriteLine();
        }
        else
        {
            renderer.EnsureLine();
        }

        string prevStyle = renderer.CurrentStyle;
        renderer.CurrentStyle = VTStyle;

        try
        {
            renderer
                .Write(VTStyle)
                .Write(new string('#', obj.Level))
                .Write(' ')
                .WriteLeafInline(obj)
                .Write(VTReset);
            
            if (prevStyle is not null)
            {
                renderer.Write(prevStyle);
            }
        }
        finally
        {
            renderer.CurrentStyle = prevStyle;
        }

        renderer.EnsureLine();
    }
}
