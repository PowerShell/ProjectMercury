// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for quote blocks.
/// </summary>
internal class QuoteBlockRenderer : VTObjectRenderer<QuoteBlock>
{
    private const char QuoteChar = '\u2502';

    protected override void Write(VTRenderer renderer, QuoteBlock obj)
    {
        renderer.EnsureLine();
        renderer.PushIndent($"  {QuoteChar} ");

        renderer.WriteChildren(obj);

        renderer.PopIndent();
        renderer.EnsureLine();
    }
}
