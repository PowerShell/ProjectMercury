using System.Text.RegularExpressions;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.CommandLine.Completions;

using Microsoft.PowerShell;
using AIShell.Abstraction;
using AIShell.Kernel.Commands;

namespace AIShell.Kernel;

internal class ReadLineHelper : IReadLineHelper
{
    private readonly CommandRunner _cmdRunner;
    private readonly Comparison<CompletionResult> _comparison;
    private readonly HashSet<string> _commonOptions;

    private readonly string _predictorName;
    private readonly Guid _predictorId;
    private readonly EnumerationOptions _enumerationOptions;

    internal ReadLineHelper(CommandRunner commandRunner)
    {
        _cmdRunner = commandRunner;
        _comparison = new(Compare);
        _commonOptions = new(StringComparer.OrdinalIgnoreCase) { "--help", "-h" };

        _predictorName = "completion";
        _predictorId = Guid.NewGuid();
        _enumerationOptions = new()
        {
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Simple,
            ReturnSpecialDirectories = false,
            RecurseSubdirectories = false,
        };
    }

    private int Compare(CompletionResult x, CompletionResult y)
    {
        if (_commonOptions.Contains(x.CompletionText))
        {
            return _commonOptions.Contains(y.CompletionText)
                ? string.Compare(x.CompletionText, y.CompletionText, ignoreCase: true)
                : 1;
        }

        if (_commonOptions.Contains(y.CompletionText))
        {
            return -1;
        }

        return string.Compare(x.CompletionText, y.CompletionText, ignoreCase: true);
    }

    private static string WildcardToRegex(string value)
    {
        return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
    }

    private List<CompletionResult> CompleteCommandName(string wildcard)
    {
        List<CompletionResult> result = null;
        Regex regex = new(WildcardToRegex(wildcard), RegexOptions.IgnoreCase);
        foreach (var entry in _cmdRunner.Commands)
        {
            string name = entry.Key;
            if (regex.IsMatch(name))
            {
                result ??= new();
                result.Add(new CompletionResult(name, name, CompletionResultType.Command, toolTip: entry.Value.Description));
            }
        }

        result?.Sort(_comparison);
        return result;
    }

    private List<CompletionResult> CompleteFileSystemPath(string wordToComplete)
    {
        if (!Path.IsPathFullyQualified(wordToComplete))
        {
            return null;
        }

        string rootPath, fileName;
        if (wordToComplete.EndsWith(Path.DirectorySeparatorChar))
        {
            rootPath = wordToComplete.TrimEnd(Path.DirectorySeparatorChar);
            fileName = "*";
        }
        else
        {
            rootPath = Path.GetDirectoryName(wordToComplete);
            fileName = Path.GetFileName(wordToComplete) + "*";
        }

        if (!Directory.Exists(rootPath))
        {
            return null;
        }

        List<CompletionResult> result = null;
        foreach (string dir in Directory.EnumerateDirectories(rootPath, fileName, _enumerationOptions))
        {
            result ??= new();
            string text = QuoteIfNeeded(dir);
            result.Add(new CompletionResult(text, text, CompletionResultType.ProviderContainer, toolTip: null));
        }

        foreach (string file in Directory.EnumerateFiles(rootPath, fileName, _enumerationOptions))
        {
            result ??= new();
            string text = QuoteIfNeeded(file);
            result.Add(new CompletionResult(text, text, CompletionResultType.ProviderItem, toolTip: null));
        }

        return result;

        static string QuoteIfNeeded(string path)
        {
            return path.Contains(' ') ? $"\"{path}\"" : path;
        }
    }

    public CommandCompletion CompleteInput(string input, int cursorIndex)
    {
        if (!input.StartsWith('/') || cursorIndex is 0)
        {
            return null;
        }

        string cmdLine = input[1..].Trim();
        if (cmdLine.Length is 0)
        {
            var matches = CompleteCommandName("*");
            return matches is null ? null : new CommandCompletion(matches, -1, cursorIndex, 0);
        }

        int offset = input.IndexOf(cmdLine);
        if (cursorIndex < offset)
        {
            return null;
        }

        int index = cmdLine.IndexOf(' ');
        string cmdName = index is -1
            ? cmdLine
            : cmdLine[0..index];

        if (cursorIndex <= offset + cmdName.Length)
        {
            var matches = CompleteCommandName($"{cmdName}*");
            return matches is null ? null : new CommandCompletion(matches, -1, offset, cmdName.Length);
        }

        if (_cmdRunner.Commands.TryGetValue(cmdName, out CommandBase command))
        {
            try
            {
                ParseResult parseResult = command.Parser.Parse(input[offset..]);
                IEnumerable<CompletionItem> items = parseResult.GetCompletions(cursorIndex - offset);

                List<CompletionResult> matches = null;
                foreach (CompletionItem item in items)
                {
                    matches ??= new();
                    matches.Add(
                        new CompletionResult(
                            item.InsertText,
                            item.Label,
                            item.InsertText.StartsWith('-')
                                ? CompletionResultType.ParameterName
                                : CompletionResultType.ParameterValue,
                            item.Detail));
                }

                matches?.Sort(_comparison);

                int tokenStartIndex = -1;
                Token tokenAtCursor = null;
                int start = offset + cmdName.Length;

                foreach (Token token in parseResult.Tokens)
                {
                    string value = token.Value;
                    int i = input.IndexOf(value, start);
                    if (cursorIndex >= i && cursorIndex <= i + value.Length)
                    {
                        tokenStartIndex = i;
                        tokenAtCursor = token;
                        break;
                    }
                }

                if (matches is null && tokenAtCursor?.Type is TokenType.Argument)
                {
                    matches = CompleteFileSystemPath(tokenAtCursor.Value);
                }

                if (matches is not null)
                {
                    int replaceIndex = cursorIndex;
                    int replaceLength = 0;

                    if (tokenAtCursor is not null)
                    {
                        replaceIndex = tokenStartIndex;
                        replaceLength = tokenAtCursor.Value.Length;
                    }

                    return new CommandCompletion(matches, -1, replaceIndex, replaceLength);
                }
            }
            catch
            {
                // Ignore all exceptions.
            }
        }

        return null;
    }

    private List<PredictionResult> PredictInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        CommandCompletion completion = CompleteInput(input, input.Length);
        if (completion is null || completion.CompletionMatches.Count is 0)
        {
            return null;
        }

        List<PredictiveSuggestion> suggestions = new();
        foreach (CompletionResult result in completion.CompletionMatches)
        {
            var span = input.AsSpan(0, completion.ReplacementIndex);
            suggestions.Add(new PredictiveSuggestion($"{span}{result.CompletionText}", result.ToolTip));
        }

        return new List<PredictionResult>()
        {
            new(_predictorId, _predictorName, session: null, suggestions)
        };
    }

    public Task<List<PredictionResult>> PredictInputAsync(string input)
    {
        return Task.Run(() => PredictInput(input));
    }
}

internal class PromptHelper : IReadLineHelper
{
    private const string GUID = "744123ff-aefe-42e9-b38b-6f5416d4c795";

    private readonly string _predictorName;
    private readonly Guid _predictorId;
    private readonly IList<string> _candidates;

    internal PromptHelper(IList<string> candidates)
    {
        _candidates = candidates;
        _predictorName = "suggestion";
        _predictorId = new Guid(GUID);
    }

    public CommandCompletion CompleteInput(string input, int cursorIndex)
    {
        if (_candidates is null || _candidates.Count is 0)
        {
            return null;
        }

        List<CompletionResult> matches = null;
        foreach (string value in _candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            {
                matches ??= [];
                matches.Add(new CompletionResult(value, value, CompletionResultType.ParameterValue, toolTip: null));
            }
        }

        return matches is null ? null : new CommandCompletion(matches, -1, replacementIndex: 0, replacementLength: input.Length);
    }

    public Task<List<PredictionResult>> PredictInputAsync(string input)
    {
        return Task.Run(() => PredictInput(input));
    }

    private List<PredictionResult> PredictInput(string input)
    {
        if (_candidates is null || _candidates.Count is 0)
        {
            return null;
        }

        int index = 0;
        List<PredictiveSuggestion> suggestions = null;
        foreach (string value in _candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            {
                suggestions ??= [];
                suggestions.Insert(index++, new PredictiveSuggestion(value));
            }
            else if (value.Contains(input, StringComparison.OrdinalIgnoreCase))
            {
                suggestions ??= [];
                suggestions.Add(new PredictiveSuggestion(value));
            }
        }

        return suggestions is null ? null : [new(_predictorId, _predictorName, session: null, suggestions)];
    }
}
