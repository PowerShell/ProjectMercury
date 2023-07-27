// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Renderers;

namespace Markdown.VT;

/// <summary>
/// Initializes an instance of the VT100 renderer.
/// </summary>
public sealed class VTRenderer : TextRendererBase<VTRenderer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VTRenderer"/> class.
    /// </summary>
    /// <param name="writer">TextWriter to write to.</param>
    /// <param name="optionInfo">PSMarkdownOptionInfo object with options.</param>
    public VTRenderer(TextWriter writer, PSMarkdownOptionInfo optionInfo) : base(writer)
    {
        EscapeSequences = new VT100EscapeSequences(optionInfo);

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
    }

    /// <summary>
    /// Gets the current escape sequences.
    /// </summary>
    public VT100EscapeSequences EscapeSequences { get; }

    internal string CurrentStyle { get; set; }
    internal bool UseSpectreMarkup { get; set; }

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
