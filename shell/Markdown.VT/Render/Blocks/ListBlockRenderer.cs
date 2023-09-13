// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Markdig.Syntax;

namespace Markdown.VT;

/// <summary>
/// Renderer for adding VT100 escape sequences for list blocks.
/// </summary>
internal class ListBlockRenderer : VTObjectRenderer<ListBlock>
{
    private const string Bullet = "\u2022 ";

    protected override void Write(VTRenderer renderer, ListBlock obj)
    {
        bool isTopLevel = obj.Parent is not ListItemBlock;
        if (isTopLevel)
        {
            renderer.WriteLine();
        }
        else
        {
            renderer.EnsureLine();
            renderer.PushIndentAndUpdateWidth("  ");
        }

        int index = 1;
        if (obj.IsOrdered && int.TryParse(obj.OrderedStart, out int value))
        {
            index = value;
        }

        foreach (ListItemBlock listItem in obj)
        {
            var prefix = obj.IsOrdered ? $"{index++}. " : Bullet;

            renderer.Write(prefix).WriteChildren(listItem);
        }

        renderer.EnsureLine();

        if (!isTopLevel)
        {
            renderer.PopIndentAndUpdateWidth();
        }
    }
}
