// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using Markdig.Syntax.Inlines;
using Spectre.Console;

namespace Markdown.VT;

/// <summary>
/// A HTML renderer for a <see cref="LiteralInline"/>.
/// </summary>
/// <seealso cref="VTObjectRenderer{LiteralInline}" />
public class LiteralInlineRenderer : VTObjectRenderer<LiteralInline>
{
    protected override void Write(VTRenderer renderer, LiteralInline obj)
    {
        if (renderer.UseSpectreMarkup)
        {
            string content = obj.Content.ToString();
            renderer.Write(content.EscapeMarkup());
        }
        else
        {
            renderer.Write(ref obj.Content);
        }
    }
}
