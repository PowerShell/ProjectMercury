using System.Collections.Generic;
using ColorCode.Common;

namespace ColorCode.VT;

public class Bash : ILanguage
{
    public string Id => "bash";
    public string Name => "bash";
    public string CssClassName => "bash";
    public string FirstLinePattern => null;

    internal const string BashCommentScope = "Bash Comment";

    public IList<LanguageRule> Rules =>
        new List<LanguageRule>
        {
            new LanguageRule(
                @"(\#.*?)\r?$",
                new Dictionary<int, string>
                {
                    {1, BashCommentScope}
                }),

            // match the first word of a line in a multi-line string as the command name.
            new LanguageRule(
                @"(?m)^\s*(\w+)",
                new Dictionary<int, string>
                {
                    {1, ScopeName.PowerShellCommand}
                }),

            // match options like '-word'
            new LanguageRule(
                @"\s+-\w+",
                new Dictionary<int, string>
                    {
                        {0, ScopeName.PowerShellParameter}
                    }
                ),

            // match options like '--word', '--word-word', '--word-word-word' and etc.
            new LanguageRule(
                @"\s+--(?:\w+-?)+",
                new Dictionary<int, string>
                    {
                        {0, ScopeName.PowerShellParameter}
                    }
                ),

            // match variable like '$word', '$digit', '$word_word' and etc.
            new LanguageRule(
                @"\$(?:[\d\w]+_?)+",
                new Dictionary<int, string>
                    {
                        {0, ScopeName.PowerShellVariable}
                    }
                ),
        };

    public bool HasAlias(string lang)
    {
        switch (lang.ToLower())
        {
            case "sh":
                return true;

            default:
                return false;
        }
    }
}
