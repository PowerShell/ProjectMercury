// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using Markdig.Syntax.Inlines;

namespace Markdown.VT;

/// <summary>
/// A HTML renderer for a <see cref="DelimiterInline"/>.
/// </summary>
/// <seealso cref="VTObjectRenderer{DelimiterInline}" />
public class DelimiterInlineRenderer : VTObjectRenderer<DelimiterInline>
{
    protected override void Write(VTRenderer renderer, DelimiterInline obj)
    {
        renderer.Write(obj.ToLiteral());
        renderer.WriteChildren(obj);
    }
}