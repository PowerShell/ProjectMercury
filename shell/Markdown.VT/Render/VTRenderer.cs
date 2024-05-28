// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;

using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Renderers;

namespace Markdown.VT;

/// <summary>
/// Initializes an instance of the VT100 renderer.
/// </summary>
public sealed class VTRenderer : TextRendererBase<VTRenderer>
{
    private readonly List<int> _indentWidth;
    private readonly IVTRenderVisitor _visitor;

    // The default indent is two space characters.
    internal const string DefaultIndent = "  ";

    public VTRenderer(TextWriter writer, PSMarkdownOptionInfo optionInfo)
        : this(writer, optionInfo, visitor: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VTRenderer"/> class.
    /// </summary>
    /// <param name="writer">TextWriter to write to.</param>
    /// <param name="optionInfo">PSMarkdownOptionInfo object with options.</param>
    public VTRenderer(TextWriter writer, PSMarkdownOptionInfo optionInfo, IVTRenderVisitor visitor) : base(writer)
    {
        _visitor = visitor;
        _indentWidth = new List<int>();
        EscapeSequences = new VT100EscapeSequences(optionInfo);

        // For all the renderers, some write out an extra line at the beginning to separate from the already
        // rendered components. However, none of them write out extra line at the end of the rendering, and
        // we should keep it that way because that makes it more predictable when we need to change the layout
        // of the rendering for any renderers.
        // Default block renderers
        ObjectRenderers.Add(new CodeBlockRenderer());
        ObjectRenderers.Add(new ListBlockRenderer());
        ObjectRenderers.Add(new HeadingBlockRenderer());
        ObjectRenderers.Add(new ParagraphBlockRenderer());
        ObjectRenderers.Add(new QuoteBlockRenderer());
        ObjectRenderers.Add(new ThematicBreakRenderer());

        // Default inline renderers
        ObjectRenderers.Add(new AutolinkInlineRenderer());
        ObjectRenderers.Add(new CodeInlineRenderer());
        ObjectRenderers.Add(new DelimiterInlineRenderer());
        ObjectRenderers.Add(new EmphasisInlineRenderer());
        ObjectRenderers.Add(new LineBreakInlineRenderer());
        ObjectRenderers.Add(new LinkInlineRenderer());
        ObjectRenderers.Add(new LiteralInlineRenderer());

        ObjectRenderers.Add(new ListItemBlockRenderer());

        // Extension renderers
        ObjectRenderers.Add(new VTTableRenderer());
        // ObjectRenderers.Add(new LeafInlineRenderer());

        PushIndentAndUpdateWidth(DefaultIndent);
    }

    /// <summary>
    /// Gets the current escape sequences.
    /// </summary>
    public VT100EscapeSequences EscapeSequences { get; }

    internal string CurrentStyle { get; set; }
    internal bool UseSpectreMarkup { get; set; }
    internal IVTRenderVisitor Visitor => _visitor;

    internal void PushIndentAndUpdateWidth(string indent)
    {
        _indentWidth.Add(indent.Length);
        PushIndent(indent);
    }

    internal void PopIndentAndUpdateWidth()
    {
        _indentWidth.RemoveAt(_indentWidth.Count - 1);
        PopIndent();
    }

    internal int GetIndentWidth()
    {
        int result = 0;
        foreach (int w in _indentWidth)
        {
            result += w;
        }
        return result;
    }

    /// <summary>
    /// Writes the lines of a <see cref="LeafBlock"/>
    /// </summary>
    /// <param name="leafBlock">The leaf block.</param>
    /// <returns>This instance</returns>
    public VTRenderer WriteLeafRawLines(LeafBlock leafBlock)
    {
        ArgumentNullException.ThrowIfNull(leafBlock);

        var slices = leafBlock.Lines.Lines;
        if (slices is not null)
        {
            int count = leafBlock.Lines.Count;
            for (int i = 0; i < count; i++)
            {
                ref StringSlice slice = ref slices[i].Slice;
                ReadOnlySpan<char> span = slice.AsSpan();
                Write(span);

                if (i == count - 1)
                {
                    WriteLine("\x1b[0m");
                }
                else
                {
                    WriteLine();
                }
            }
        }

        return this;
    }
}

public interface IVTRenderVisitor
{
    public void VisitCodeBlock(string code, string language);
}
