// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax.Inlines;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for links.
/// </summary>
internal class LinkInlineRenderer : VTObjectRenderer<LinkInline>
{
    private const string VTImageLabelStyle = "\x1b[38;5;243m";
    private const string VTImageUrlStyle = "\x1b[38;5;212;4m";
    private const string VTLinkStyle = "\x1b[38;5;35;1m";
    private const string VTReset = "\x1b[0m";

    private const string MarkupImageLabelStyle = "[rgb(118,118,118)]";
    private const string MarkupImageUrlStyle = "[underline rgb(255,135,215)]";
    private const string MarkupLinkStyle = "[bold rgb(0,175,95)]";
    private const string MarkupReset = "[/]";

    public string HyperlinkInVT(string text, string url)
    {
        return $"\x1b]8;;{url}\x1b\\{text}\x1b]8;;\x1b\\";
    }

    public string HyperlinkInMarkup(string text, string url)
    {
        return $"[link={url}]{text}[/]";
    }

    protected override void Write(VTRenderer renderer, LinkInline obj)
    {
        string url = obj.GetDynamicUrl is not null ? obj.GetDynamicUrl() ?? obj.Url : obj.Url;
        string prevStyle = renderer.CurrentStyle;
        bool useMarkup = renderer.UseSpectreMarkup;

        if (obj.IsImage)
        {
            if (obj.Label is null)
            {
                renderer
                    .Write(useMarkup ? MarkupImageUrlStyle : VTImageUrlStyle)
                    .Write(url)
                    .Write(useMarkup ? MarkupReset : VTReset);
            }
            else
            {
                renderer
                    .Write(useMarkup ? MarkupImageLabelStyle : VTImageLabelStyle)
                    .Write("Image: ")
                    .Write(useMarkup ? HyperlinkInMarkup(obj.Label, url) : HyperlinkInVT(obj.Label, url))
                    .Write(useMarkup ? MarkupReset : VTReset);
            }
        }
        else if (useMarkup)
        {
            renderer
                .Write(MarkupLinkStyle)
                .Write($"[link={url}]")
                .WriteChildren(obj);
            renderer
                .Write(MarkupReset)
                .Write(MarkupReset);
        }
        else
        {
            renderer
                .Write(VTLinkStyle)
                .Write($"\x1b]8;;{url}\x1b\\")
                .WriteChildren(obj);
            renderer
                .Write($"\x1b]8;;\x1b\\")
                .Write(VTReset);
        }

        if (!useMarkup && prevStyle is not null)
        {
            renderer.Write(prevStyle);
        }
    }
}
