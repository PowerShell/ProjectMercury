using System.Collections.ObjectModel;
using System.Management.Automation;
using AISH.Abstraction;

namespace AISH.Integration;

[Alias("askai")]
[Cmdlet(VerbsLifecycle.Invoke, "Aish", DefaultParameterSetName = "Default")]
public class InvokeAishCommand : PSCmdlet
{
    [Parameter(Position = 0, Mandatory = true)]
    public string Query { get; set; }

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Agent { get; set; }

    [Parameter(ParameterSetName = "Default", Position = 1, Mandatory = false, ValueFromPipeline = true)]
    public PSObject Context { get; set; }

    [Parameter(ParameterSetName = "Clipboard", Mandatory = true)]
    public SwitchParameter ContextFromClipboard { get; set; }

    private List<PSObject> _contextObjects;

    protected override void ProcessRecord()
    {
        if (Context is null)
        {
            return;
        }

        _contextObjects ??= [];
        _contextObjects.Add(Context);
    }

    protected override void EndProcessing()
    {
        Collection<string> results = null;
        if (_contextObjects is not null)
        {
            using PowerShell pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace);
            results = pwsh
                .AddCommand("Out-String")
                .AddParameter("InputObject", _contextObjects)
                .Invoke<string>();
        }
        else if (ContextFromClipboard)
        {
            using PowerShell pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace);
            results = pwsh
                .AddCommand("Get-Clipboard")
                .AddParameter("Raw")
                .Invoke<string>();
        }

        string context = results?.Count > 0 ? results[0] : null;
        AishChannel.Singleton.PostQuery(new PostQueryMessage(Query, context, Agent));
    }
}
