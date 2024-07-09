using Microsoft.PowerShell;
using AIShell.Abstraction;

namespace AIShell.Kernel;

internal partial class Shell
{
    public string AssistParameterInjection(ArgumentInfo argInfo)
    {
        Host.WriteLine($"{argInfo.Name}: {argInfo.Description}.");
        if (!string.IsNullOrEmpty(argInfo.Restriction))
        {
            Host.WriteLine(argInfo.Restriction);
        }

        if (argInfo.Type is ArgumentInfo.DataType.Bool || argInfo.Suggestions?.Count is 2)
        {
            return Host.PromptForTextAsync(
                prompt: ">",
                optional: false,
                choices: argInfo.Suggestions ?? ["ture", "flase"],
                cancellationToken: CancellationToken).GetAwaiter().GetResult();
        }

        if (argInfo.MustChooseFromSuggestions)
        {
            string value = Host.PromptForSelectionAsync(
                title: "Choose the value from the below list:",
                choices: argInfo.Suggestions,
                cancellationToken: CancellationToken).GetAwaiter().GetResult();
            Host.WriteLine($"> {value}");
            return value;
        }

        var options = PSConsoleReadLine.GetOptions();
        var oldReadLineHelper = options.ReadLineHelper;
        var oldPredictionView = options.PredictionViewStyle;
        var oldPredictionSource = options.PredictionSource;

        var newOptions = new SetPSReadLineOption
        {
            ReadLineHelper = new PromptHelper(argInfo.Suggestions),
            PredictionSource = PredictionSource.Plugin,
            PredictionViewStyle = PredictionViewStyle.ListView,
        };

        try
        {
            Host.Write("> ");
            PSConsoleReadLine.SetOptions(newOptions);
            string value = PSConsoleReadLine.ReadLine();
            if (Console.CursorLeft is not 0)
            {
                // Ctrl+c was pressed by the user.
                Host.WriteLine();
                throw new OperationCanceledException();
            }

            return value;
        }
        finally
        {
            newOptions.ReadLineHelper = oldReadLineHelper;
            newOptions.PredictionSource = oldPredictionSource;
            newOptions.PredictionViewStyle = oldPredictionView;
            PSConsoleReadLine.SetOptions(newOptions);
        }
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
        _predictorName = "completion";
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
