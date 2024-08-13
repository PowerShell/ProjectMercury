// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax.Inlines;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for inline code elements.
/// </summary>
internal class CodeInlineRenderer : VTObjectRenderer<CodeInline>
{
    // Use extended color for background.
    // See https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors
    private const string VTStyle = "\x1b[92;48;5;236m";
    private const string VTReset = "\x1b[0m";
    private const string MarkupStyle = "[rgb(0,195,0) on rgb(48,48,48)]";
    private const string MarkupReset = "[/]";

    protected override void Write(VTRenderer renderer, CodeInline obj)
    {
        bool useMarkup = renderer.UseSpectreMarkup;

        renderer.Write(useMarkup ? MarkupStyle : VTStyle);
        renderer.Write(' ');
        renderer.Write(obj.ContentSpan);
        renderer.Write(' ');
        renderer.Write(useMarkup ? MarkupReset : VTReset);
    }
}
