using System.Text.RegularExpressions;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.CommandLine.Completions;

using Microsoft.PowerShell;
using ShellCopilot.Kernel.Commands;

namespace ShellCopilot.Kernel;

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
                result.Add(new CompletionResult(name, name, CompletionResultType.Command, toolTip: null));
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
