// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using Markdig.Syntax;

namespace Markdown.VT;

/// <summary>
/// A HTML renderer for a <see cref="ThematicBreakBlock"/>.
/// </summary>
/// <seealso cref="VTObjectRenderer{ThematicBreakBlock}" />
public class ThematicBreakRenderer : VTObjectRenderer<ThematicBreakBlock>
{
    private const string VTReset = "\x1b[0m";
    private const string Style = "\x1b[38;5;240m";

    protected override void Write(VTRenderer renderer, ThematicBreakBlock obj)
    {
        renderer.WriteLine();
        renderer.WriteLine($"{Style}--------{VTReset}");
    }
}
