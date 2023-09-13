// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using Markdig.Syntax.Inlines;

namespace Markdown.VT;

/// <summary>
/// A HTML renderer for an <see cref="AutolinkInline"/>.
/// </summary>
/// <seealso cref="VTObjectRenderer{AutolinkInline}" />
public class AutolinkInlineRenderer : VTObjectRenderer<AutolinkInline>
{
    public string HyperlinkInVT(string text, string url)
    {
        return $"\x1b]8;;{url}\x1b\\{text}\x1b]8;;\x1b\\";
    }

    protected override void Write(VTRenderer renderer, AutolinkInline obj)
    {
        if (!obj.IsEmail)
        {
            renderer.Write(obj.Url);
            return;
        }

        renderer.Write(HyperlinkInVT(obj.Url, $"mailtto:{obj.Url}"));
    }
}
