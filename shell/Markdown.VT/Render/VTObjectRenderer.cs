// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Renderers;
using Markdig.Syntax;

namespace Markdown.VT;

/// <summary>
/// A base class for VT rendering <see cref="Block"/> and <see cref="Markdig.Syntax.Inlines.Inline"/> Markdown objects.
/// </summary>
/// <typeparam name="T">The element type of the renderer.</typeparam>
public abstract class VTObjectRenderer<T> : MarkdownObjectRenderer<VTRenderer, T> where T : MarkdownObject
{
}
