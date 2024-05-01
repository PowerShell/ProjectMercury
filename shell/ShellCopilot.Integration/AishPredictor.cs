using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;

namespace ShellCopilot.Integration;

public sealed class AishPredictor : ICommandPredictor
{
    internal const string GUID = "F4CEBE0C-AB0C-4F9B-B24D-CB911EA6DB29";
    private readonly Guid _guid;
    private List<string> _candidates;

    internal AishPredictor()
    {
        _guid = new Guid(GUID);
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
            if (_candidates is not null)
            {
                string input = context.InputAst.Extent.Text;
                List<PredictiveSuggestion> result = null;

                string c = _candidates[0];
                if (c.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                {
                    result = [new PredictiveSuggestion(c)];
                }

                if (result is not null)
                {
                    return new SuggestionPackage(result);
                }
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
            _candidates.RemoveAt(0);
            if (_candidates.Count is 0)
            {
                _candidates = null;
            }
        }
    }

    internal void SetCandidates(List<string> candidates)
    {
        lock (this)
        {
            _candidates = candidates;
        }
    }

    internal void Unregister()
    {
        SubsystemManager.UnregisterSubsystem<ICommandPredictor>(_guid);
    }
}
