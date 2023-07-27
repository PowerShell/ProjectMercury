// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax.Inlines;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for bold and italics elements.
/// </summary>
internal class EmphasisInlineRenderer : VTObjectRenderer<EmphasisInline>
{
    private const string VTBold = "\x1b[1m";
    private const string VTItalic = "\x1b[3m";
    private const string VTReset = "\x1b[0m";

    private const string MarkupBold = "[bold]";
    private const string MarkupItalic = "[italic]";
    private const string MarkupReset = "[/]";

    protected override void Write(VTRenderer renderer, EmphasisInline obj)
    {
        bool useMarkup = renderer.UseSpectreMarkup;
        string prevStyle = renderer.CurrentStyle;
        string currStyle = obj.DelimiterCount is 2
            ? (useMarkup ? MarkupBold : VTBold)
            : (useMarkup ? MarkupItalic : VTItalic);

        try
        {
            if (!useMarkup)
            {
                renderer.CurrentStyle = currStyle;
            }

            renderer.Write(currStyle).WriteChildren(obj);
            renderer.Write(useMarkup ? MarkupReset : VTReset);

            if (!useMarkup && prevStyle is not null)
            {
                renderer.Write(prevStyle);
            }
        }
        finally
        {
            if (!useMarkup)
            {
                renderer.CurrentStyle = prevStyle;
            }
        }
    }
}
