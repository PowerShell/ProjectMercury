using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;

namespace ShellCopilot.Integration;

public sealed class AishPredictor : ICommandPredictor
{
    private const int MaxRoundsToInvalidate = 10;
    private const string GUID = "F4CEBE0C-AB0C-4F9B-B24D-CB911EA6DB29";

    private readonly Guid _guid;

    private int _invalidationCount;
    private List<PredictionCandidate> _candidates;

    internal AishPredictor()
    {
        _guid = new Guid(GUID);
        _invalidationCount = -1;
        _candidates = null;
        SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, this);
    }

    Dictionary<string, string> ISubsystem.FunctionsToDefine => null;

    public Guid Id => _guid;

    public string Name => "aish";

    public string Description => "Provide command-line prediction by leveraging AI agents running in aish.";

    public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback)
    {
        return feedback switch
        {
            PredictorFeedbackKind.CommandLineAccepted => true,
            _ => false,
        };
    }

    public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
    {
        if (_candidates is null)
        {
            return default;
        }

        lock (this)
        {
            string input = context.InputAst.Extent.Text;
            List<PredictiveSuggestion> result = null;

            foreach (var candidate in _candidates)
            {
                if (candidate.Code.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                {
                    result ??= [];
                    result.Add(new PredictiveSuggestion(candidate.Code, candidate.Tooltip));
                }
            }

            if (result is not null)
            {
                return new SuggestionPackage(result);
            }
        }

        return default;
    }

    public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history)
    {
        if (_candidates is null)
        {
            return;
        }

        lock (this)
        {
            string lastCmdLine = history[^1];
            if (!lastCmdLine.Contains('\n'))
            {
                int longestCount = 0, longestMatchIndex = -1, longestItems = 0;
                var input = new PredictionCandidate(lastCmdLine);

                for (int i = 0; i < _candidates.Count; i++)
                {
                    int count = input.GetMatchingSectionCount(_candidates[i]);
                    if (count > longestCount)
                    {
                        longestCount = count;
                        longestMatchIndex = i;
                        longestItems = 1;
                    }
                    else if (count == longestCount)
                    {
                        longestItems++;
                    }
                }

                if (longestMatchIndex >= 0)
                {
                    // Update the candidate list if we do have candidate items that can match the input.
                    if (longestItems is 1)
                    {
                        // The longest match is likely what the user has accepted, altered arguments, and then ran.
                        // In this case we will remove this item and all its prior items from our candidate list.
                        // Even if the execution fails, it's more convenient for the user to edit based on the history entry at this point.
                        _candidates.RemoveRange(0, longestMatchIndex + 1);
                    }
                    else
                    {
                        // When there are multiple longest matches, we invalidate all items prior to the first longest match,
                        // but keep the first longest match item in the candidate list.
                        _candidates.RemoveRange(0, longestMatchIndex);
                    }

                    if (_candidates.Count is 0)
                    {
                        // Reset the fields.
                        _invalidationCount = -1;
                        _candidates = null;
                    }
                    else
                    {
                        // Update the invalidation count according to the current size of the candidate list.
                        _invalidationCount = Math.Min(_candidates.Count * 2, MaxRoundsToInvalidate);
                    }

                    return;
                }
            }

            // Update invalidation count when the input is a multi-line string, or when it doesn't match any candidates.
            _invalidationCount--;
            if (_invalidationCount is -1)
            {
                _candidates = null;
            }
        }
    }

    internal void SetCandidates(List<PredictionCandidate> candidates)
    {
        lock (this)
        {
            _candidates = candidates;
            _invalidationCount = candidates is null ? -1 : Math.Min(_candidates.Count * 2, MaxRoundsToInvalidate);
        }
    }

    internal void Unregister()
    {
        SubsystemManager.UnregisterSubsystem<ICommandPredictor>(_guid);
    }

    internal static bool TryProcessForPrediction(List<string> codeBlocks, out List<PredictionCandidate> candidates)
    {
        const char NewLine = '\n';
        const char HashTag = '#';
        candidates = null;

        foreach (string code in codeBlocks)
        {
            int index = code.IndexOf(NewLine);
            if (index is -1)
            {
                // It's a pure one-liner.
                candidates ??= [];
                candidates.Add(new PredictionCandidate(code.Trim()));
            }
            else
            {
                var firstLine = code.AsSpan(0, index).Trim();
                if (firstLine[0] is HashTag && code.IndexOf(NewLine, startIndex: index + 1) is -1)
                {
                    // It contains 2 lines and the first line is a comment.
                    // So, it's actually still a one-liner.
                    int i = 0;
                    for (; i < firstLine.Length; i++)
                    {
                        if (firstLine[i] is not HashTag)
                        {
                            break;
                        }
                    }

                    // Skip the hashtag chars to get the tooltip.
                    string tooltip = firstLine[i..].TrimStart().ToString();
                    string oneLiner = code.AsSpan(index + 1).Trim().ToString();

                    candidates ??= [];
                    candidates.Add(new PredictionCandidate(oneLiner, tooltip));
                }
                else
                {
                    // It's not a one-liner, and thus we cannot use it for prediction.
                    candidates = null;
                    return false;
                }
            }
        }

        return true;
    }
}

internal class PredictionCandidate
{
    internal string Code { get; }
    internal string Tooltip { get; }
    internal List<Range> Ranges { get; }

    internal PredictionCandidate(string code, string tooltip = null)
    {
        Code = code;
        Tooltip = tooltip;
        Ranges = SplitToRanges(code);
    }

    private static List<Range> SplitToRanges(string code)
    {
        const char SpaceChar = ' ';
        int startIndex = 0, spaceIndex;
        List<Range> ranges = [];

        while (true)
        {
            spaceIndex = code.IndexOf(SpaceChar, startIndex: startIndex);
            if (spaceIndex is -1)
            {
                if (!code.AsSpan(startIndex).IsEmpty)
                {
                    ranges.Add(new Range(
                        new Index(startIndex),
                        new Index(code.Length)));
                }
                break;
            }

            if (spaceIndex == startIndex)
            {
                // Starts with space or space chars in a row.
                startIndex++;
                continue;
            }

            ranges.Add(new Range(
                new Index(startIndex),
                new Index(spaceIndex)));

            startIndex = spaceIndex + 1;
        }

        return ranges;
    }

    /// <summary>
    /// Get the count of continuously matching sections between two candiates.
    /// </summary>
    internal int GetMatchingSectionCount(PredictionCandidate candidate)
    {
        int count = 0;
        int total = Math.Min(Ranges.Count, candidate.Ranges.Count);

        for (int i = 0; i < total; i++)
        {
            var @this = Code.AsSpan(Ranges[i]);
            var other = candidate.Code.AsSpan(candidate.Ranges[i]);

            if (@this.Equals(other, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
            else
            {
                break;
            }
        }

        return count;
    }
}
