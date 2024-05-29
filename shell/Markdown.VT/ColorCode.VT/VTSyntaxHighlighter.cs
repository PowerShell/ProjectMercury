// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using ColorCode.Common;
using ColorCode.Parsing;
using ColorCode.Styling;

namespace ColorCode.VT;

/// <summary>
/// Creates a <see cref="VTSyntaxHighlighter"/>, for creating VT decorated string to display Syntax Highlighted code.
/// </summary>
public class VTSyntaxHighlighter : CodeColorizerBase
{
    internal const string VTReset = "\x1b[0m";
    internal const string VTItalic = "\x1b[3m";
    internal const string VTBold = "\x1b[1m";
    internal const string VTEraseRestOfLine = "\x1b[K";

    private readonly string _plainFgBgColors;
    private readonly StringBuilder _buffer;

    /// <summary>
    /// Creates a <see cref="VTSyntaxHighlighter"/>, for creating VT decorated string to display Syntax Highlighted code.
    /// </summary>
    /// <param name="styles">The Custom styles to Apply to the formatted Code.</param>
    /// <param name="languageParser">The language parser that the <see cref="VTSyntaxHighlighter"/> instance will use for its lifetime.</param>
    public VTSyntaxHighlighter(StyleDictionary styles = null, ILanguageParser languageParser = null)
        : base(styles.UseCustomStyle(), languageParser)
    {
        _buffer = new StringBuilder(capacity: 512);

        if (styles.TryGetValue(ScopeName.PlainText, out Style style))
        {
            string foreground = style.Foreground.ToVTColor();
            string background = style.Background.ToVTColor(isForeground: false);
            _plainFgBgColors = $"{foreground}{background}";
        }

        Languages.Load(new Bash());
        Languages.Load(new Json());
        Languages.Load(new PowerShell());
    }

    /// <summary>
    /// Creates the VT decorated string.
    /// </summary>
    /// <param name="sourceCode">The source code to colorize.</param>
    /// <param name="language">The language to use to colorize the source code.</param>
    /// <returns>VT decorated string.</returns>
    public string GetVTString(string sourceCode, ILanguage language)
    {
        try
        {
            // Normalize line endings to always be LF only.
            sourceCode = sourceCode.Replace("\r\n", "\n");
            _buffer.Append(_plainFgBgColors);

            // Only apply foreground and background colors when no language specified.
            // Otherwise, render the code block based on the language.
            if (language is null)
            {
                WriteText(sourceCode, inCapturedScope: false);
            }
            else
            {
                languageParser.Parse(sourceCode, language, Write);
            }

            return _buffer
                .Append(VTEraseRestOfLine)
                .Append(VTReset)
                .ToString();
        }
        finally
        {
            _buffer.Clear();
        }
    }

    protected override void Write(string sourceCode, IList<Scope> scopes)
    {
        int offset = 0;
        bool inCapturedScope = false;

        if (scopes.Count > 0)
        {
            var styles = new List<TextInsertion>();
            foreach (Scope scope in scopes)
            {
                GetStyleForCapturedScope(scope, styles, isChild: false);
            }

            styles.SortStable((x, y) => x.Index.CompareTo(y.Index));

            foreach (TextInsertion style in styles)
            {
                var text = sourceCode.AsSpan(offset, style.Index - offset);
                WriteText(text, inCapturedScope);

                if (string.IsNullOrEmpty(style.Text))
                {
                    WriteCapturedStyle(style.Scope);
                    inCapturedScope = true;
                }
                else
                {
                    _buffer.Append(style.Text);
                    inCapturedScope = false;
                }

                offset = style.Index;
            }
        }

        WriteText(sourceCode.AsSpan(offset), inCapturedScope);
    }

    private void WriteText(ReadOnlySpan<char> buffer, bool inCapturedScope)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            char c = buffer[i];

            if (c is '\n')
            {
                // Erase the rest of line so the rest of line can be redrawn with the default background color.
                _buffer.Append(VTEraseRestOfLine);
                _buffer.Append(c);

                if (!inCapturedScope)
                {
                    // Write the default foreground and background colors right after a newline to make it possbile
                    // to re-render part of the VT decorated code block from a newline character.
                    // We only do this when we are not in a captured scope because the color settings should not be
                    // interrupted within a captured scope.
                    _buffer.Append(_plainFgBgColors);
                }

                continue;
            }

            _buffer.Append(c);
        }
    }

    private void GetStyleForCapturedScope(Scope scope, ICollection<TextInsertion> styles, bool isChild)
    {
        styles.Add(new TextInsertion { Index = scope.Index, Scope = scope });

        foreach (Scope childScope in scope.Children)
        {
            GetStyleForCapturedScope(childScope, styles, isChild: true);
        }

        if (!isChild)
        {
            styles.Add(new TextInsertion
            {
                Index = scope.Index + scope.Length,
                Text = $"{VTReset}{_plainFgBgColors}"
            });
        }
    }

    private void WriteCapturedStyle(Scope scope)
    {
        if (!Styles.TryGetValue(scope.Name, out Style style))
        {
            return;
        }

        // To make the code block rendering have a consistent view, we should always use the same default background color.
        // So, we do not use the background color definition here even if it's set. The syntax color setting should only
        // cares about forground color and font effect.
        if (!string.IsNullOrWhiteSpace(style.Foreground))
        {
            _buffer.Append(style.Foreground.ToVTColor());
        }

        if (style.Italic)
        {
            _buffer.Append(VTItalic);
        }

        if (style.Bold)
        {
            _buffer.Append(VTBold);
        }
    }
}

public static class ColorExtensionMethods
{
    /// <summary>
    /// Use the PSReadLine syntax colors for PowerShell syntax highlighting.
    /// </summary>
    internal static StyleDictionary UseCustomStyle(this StyleDictionary styles)
    {
        if (styles is null)
        {
            return null;
        }

        styles.Remove(ScopeName.String);
        styles.Remove(ScopeName.Comment);
        styles.Remove(ScopeName.PowerShellCommand);
        styles.Remove(ScopeName.PowerShellOperator);
        styles.Remove(ScopeName.PowerShellParameter);
        styles.Remove(ScopeName.PowerShellVariable);

        styles.Add(
            new Style(ScopeName.String)
            {
                Foreground = "\x1b[36m",
                ReferenceName = "string"
            });
        styles.Add(
            new Style(ScopeName.Comment)
            {
                Foreground = "\x1b[32m",
                ReferenceName = "comment"
            });
        styles.Add(
            new Style(ScopeName.PowerShellCommand)
            {
                Foreground = "\x1b[93m",
                ReferenceName = "powerShellCommand"
            });
        styles.Add(
            new Style(ScopeName.PowerShellOperator)
            {
                Foreground = "\x1b[90m",
                ReferenceName = "powershellOperator"
            });
        styles.Add(
            new Style(ScopeName.PowerShellParameter)
            {
                Foreground = "\x1b[90m",
                ReferenceName = "powerShellParameter"
            });
        styles.Add(
            new Style(ScopeName.PowerShellVariable)
            {
                Foreground = "\x1b[92m",
                ReferenceName = "powershellVariable"
            });
        styles.Add(
            new Style(Bash.BashCommentScope)
            {
                Foreground = "\x1b[90m",
                ReferenceName = "bashComment"
            });

        return styles;
    }

    public static string ToVTColor(this string color, bool isForeground = true)
    {
        if (color == null)
        {
            return null;
        }

        if (color.StartsWith("\x1b["))
        {
            return color;
        }

        if (color.StartsWith('#'))
        {
            var length = 6;
            var start = color.Length - length;
            var colorSpan = color.AsSpan(start, length);

            if (int.TryParse(colorSpan, NumberStyles.HexNumber, provider: null, out int result))
            {
                return isForeground ? ForegroundFromRgb(result) : BackgroundFromRgb(result);
            }
        }

        return null;
    }

    internal static string ForegroundFromRgb(int rgb)
    {
        byte red, green, blue;
        blue = (byte)(rgb & 0xFF);
        rgb >>= 8;
        green = (byte)(rgb & 0xFF);
        rgb >>= 8;
        red = (byte)(rgb & 0xFF);

        return $"\x1b[38;2;{red};{green};{blue}m";
    }

    public static string ForegroundFromRgb(byte red, byte green, byte blue)
    {
        return $"\x1b[38;2;{red};{green};{blue}m";
    }

    internal static string BackgroundFromRgb(int rgb)
    {
        byte red, green, blue;
        blue = (byte)(rgb & 0xFF);
        rgb >>= 8;
        green = (byte)(rgb & 0xFF);
        rgb >>= 8;
        red = (byte)(rgb & 0xFF);

        return $"\x1b[48;2;{red};{green};{blue}m";
    }

    public static string BackgroundFromRgb(byte red, byte green, byte blue)
    {
        return $"\x1b[48;2;{red};{green};{blue}m";
    }
}
