// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ColorCode.Common;

namespace ColorCode.VT;

public class Json : ILanguage
{
    private const string Regex_String = @"""([^""\\]|\\.)*""";
    private const string Regex_Number = @"-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][-+]?[0-9]+)?";

    public string Id
    {
        get { return LanguageId.Json; }
    }

    public string Name
    {
        get { return "JSON"; }
    }

    public string CssClassName
    {
        get { return "json"; }
    }

    public string FirstLinePattern
    {
        get { return null; }
    }

    public IList<LanguageRule> Rules
    {
        get
        {
            return new List<LanguageRule>
                    {
                        new LanguageRule(
                            $@"[,\{{]\s*({Regex_String})\s*:",
                            new Dictionary<int, string>
                                {
                                    {1, ScopeName.JsonKey}
                                }),
                        new LanguageRule(
                            Regex_String,
                            new Dictionary<int, string>
                                {
                                    {0, ScopeName.JsonString}
                                }),
                        new LanguageRule(
                            Regex_Number,
                            new Dictionary<int, string>
                                {
                                    {0, ScopeName.JsonNumber}
                                }),
                        new LanguageRule(
                            @"\b(true|false|null)\b",
                            new Dictionary<int, string>
                                {
                                    {1, ScopeName.JsonConst}
                                }),
                    };
        }
    }

    public bool HasAlias(string lang)
    {
        return false;
    }
}
