// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

    private readonly string _plainTextForeground;
    private readonly string _plainTextBackground;

    private readonly ILanguage[] _loadedLanguages;

    /// <summary>
    /// Creates a <see cref="VTSyntaxHighlighter"/>, for creating VT decorated string to display Syntax Highlighted code.
    /// </summary>
    /// <param name="styles">The Custom styles to Apply to the formatted Code.</param>
    /// <param name="languageParser">The language parser that the <see cref="VTSyntaxHighlighter"/> instance will use for its lifetime.</param>
    public VTSyntaxHighlighter(StyleDictionary styles = null, ILanguageParser languageParser = null)
        : base(styles, languageParser)
    {
        if (styles.TryGetValue(ScopeName.PlainText, out Style style))
        {
            _plainTextForeground = style.Foreground.ToVTColor();
            _plainTextBackground = style.Background.ToVTColor(isForeground: false);
        }

        _loadedLanguages = new ILanguage[0];
    }

    private TextWriter Writer { get; set; }

    /// <summary>
    /// Finds a loaded language by the specified identifier.
    /// </summary>
    public ILanguage FindLanguageById(string langId)
    {
        if (string.IsNullOrEmpty(langId))
        {
            return null;
        }

        foreach (ILanguage lang in _loadedLanguages)
        {
            if (lang.Id.ToLower() == langId.ToLower() || lang.HasAlias(langId))
            {
                return lang;
            }
        }

        return Languages.FindById(langId);
    }

    /// <summary>
    /// Creates the VT decorated string.
    /// </summary>
    /// <param name="sourceCode">The source code to colorize.</param>
    /// <param name="language">The language to use to colorize the source code.</param>
    /// <returns>VT decorated string.</returns>
    public string GetVTString(string sourceCode, ILanguage language)
    {
        var buffer = new StringBuilder(sourceCode.Length * 2);
        buffer.Append(_plainTextForeground).Append(_plainTextBackground);

        using (TextWriter writer = new StringWriter(buffer))
        {
            Writer = writer;
            languageParser.Parse(sourceCode, language, Write);
            Writer.Flush();
        }

        buffer.Append(VTReset);
        return buffer.ToString();
    }

    protected override void Write(string parsedSourceCode, IList<Scope> scopes)
    {
        var styleInsertions = new List<TextInsertion>();

        foreach (Scope scope in scopes)
        {
            GetStyleInsertionsForCapturedStyle(scope, styleInsertions);
        }

        styleInsertions.SortStable((x, y) => x.Index.CompareTo(y.Index));

        int offset = 0;

        foreach (TextInsertion styleInsertion in styleInsertions)
        {
            var text = parsedSourceCode.AsSpan(offset, styleInsertion.Index - offset);
            Writer.Write(text);
            if (string.IsNullOrEmpty(styleInsertion.Text))
            {
                BuildSpanForCapturedStyle(styleInsertion.Scope);
            }
            else
            {
                Writer.Write(styleInsertion.Text);
            }
            offset = styleInsertion.Index;
        }

        Writer.Write(parsedSourceCode.AsSpan(offset));
    }

    private void GetStyleInsertionsForCapturedStyle(Scope scope, ICollection<TextInsertion> styleInsertions)
    {
        styleInsertions.Add(new TextInsertion
        {
            Index = scope.Index,
            Scope = scope
        });

        foreach (Scope childScope in scope.Children)
        {
            GetStyleInsertionsForCapturedStyle(childScope, styleInsertions);
        }

        styleInsertions.Add(new TextInsertion
        {
            Index = scope.Index + scope.Length,
            Text = $"{VTReset}{_plainTextForeground}{_plainTextBackground}"
        });
    }

    private void BuildSpanForCapturedStyle(Scope scope)
    {
        string foreground = null;
        string background = null;
        bool italic = false;
        bool bold = false;

        if (Styles.Contains(scope.Name))
        {
            Style style = Styles[scope.Name];

            foreground = style.Foreground;
            background = style.Background;
            italic = style.Italic;
            bold = style.Bold;
        }

        foreground ??= _plainTextForeground;
        background ??= _plainTextBackground;

        WriteVTDecoration(foreground, background, italic, bold);
    }

    private void WriteVTDecoration(string foreground = null, string background = null, bool italic = false, bool bold = false)
    {
        if (!string.IsNullOrWhiteSpace(foreground))
        {
            Writer.Write(foreground.ToVTColor());
        }
        
        if (!string.IsNullOrWhiteSpace(background))
        {
            Writer.Write(background.ToVTColor(isForeground: false));
        }

        if (italic)
        {
            Writer.Write(VTItalic);
        }

        if (bold)
        {
            Writer.Write(VTBold);
        }
    }
}

public static class ExtensionMethods
{
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
}
