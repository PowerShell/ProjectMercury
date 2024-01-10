using System.Collections.Generic;
using ColorCode.Common;

namespace ColorCode.VT;

public class Bash : ILanguage
{
    public string Id => "bash";
    public string Name => "bash";
    public string CssClassName => "bash";
    public string FirstLinePattern => null;

    public IList<LanguageRule> Rules =>
        new List<LanguageRule>
        {
            new LanguageRule(
                @"(\#.*?)\r?$",
                new Dictionary<int, string>
                {
                    {1, ScopeName.Comment}
                }),
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
