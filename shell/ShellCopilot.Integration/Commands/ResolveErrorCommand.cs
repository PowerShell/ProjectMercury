using System.Collections;
using System.Management.Automation;
using Microsoft.PowerShell.Commands;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Integration;

[Alias("fixit")]
[Cmdlet(VerbsDiagnostic.Resolve, "Error")]
public class ResolveErrorCommand : PSCmdlet
{
    [Parameter]
    public SwitchParameter IncludeOutputFromClipboard { get; set; }

    protected override void EndProcessing()
    {
        bool questionMarkValue = (bool)GetVariableValue("?");
        if (questionMarkValue)
        {
            WriteWarning("No error to resolve. The last command execution was successful.");
            return;
        }

        object value = GetVariableValue("LASTEXITCODE");
        int lastExitCode = value is null ? 0 : (int)value;

        using var pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace);
        var results = pwsh
            .AddCommand("Get-History")
            .AddParameter("Count", 1)
            .Invoke<HistoryInfo>();

        if (results.Count is 0)
        {
            WriteWarning("No error to resolve. No command line has been executed yet.");
            return;
        }

        string query = null, context = null;
        HistoryInfo lastHistory = results[0];
        AishChannel channel = AishChannel.Singleton;
        string commandLine = lastHistory.CommandLine;

        if (TryGetLastError(lastHistory, out ErrorRecord lastError))
        {
            query = AishErrorFeedback.CreateQueryForError(commandLine, lastError, channel);
        }
        else if (lastExitCode is 0)
        {
            // Cannot find the ErrorRecord associated with the last command, and no native command failure, so we don't know why '$?' was set to False.
            ErrorRecord error = new(
                new NotSupportedException($"Failed to detect the actual error even though '$?' is 'False'. No 'ErrorRecord' can be found that is associated with the last command line '{commandLine}' and no executable failure was found."),
                errorId: "FailedToDetectActualError",
                ErrorCategory.ObjectNotFound,
                targetObject: null);
            ThrowTerminatingError(error);
        }
        else if (UseClipboardForCommandOutput(lastExitCode))
        {
            // '$? == False' but no 'ErrorRecord' can be found that is associated with the last command line,
            // and '$LASTEXITCODE' is non-zero, which indicates the last failed command is a native command.
            query = $"""
                Running the command line `{commandLine}` in PowerShell v{channel.PSVersion} failed.
                Please try to explain the failure and suggest the right fix.
                Output of the command line can be found in the context information below.
                """;
            IncludeOutputFromClipboard = true;
        }
        else
        {
            ThrowTerminatingError(new(
                new NotSupportedException($"The output content is needed for suggestions on native executable failures."),
                errorId: "OutputNeededForNativeCommand",
                ErrorCategory.InvalidData,
                targetObject: null
            ));
        }

        if (IncludeOutputFromClipboard)
        {
            pwsh.Commands.Clear();
            var r = pwsh
                .AddCommand("Get-Clipboard")
                .AddParameter("Raw")
                .Invoke<string>();

            context = r?.Count > 0 ? r[0] : null;
        }

        channel.PostQuery(new PostQueryMessage(query, context));
    }

    private bool UseClipboardForCommandOutput(int lastExitCode)
    {
        if (IncludeOutputFromClipboard)
        {
            return true;
        }

        string query = "The last failed command is a native command that didn not produce an ErrorRecord object.\nPlease \x1b[93mcopy its output and then press 'y'\x1b[0m to allow using the output as context information.";
        return ShouldContinue(query, caption: "Include output from the clipboard");
    }

    private bool TryGetLastError(HistoryInfo lastHistory, out ErrorRecord lastError)
    {
        lastError = null;
        ArrayList errors = (ArrayList)GetVariableValue("Error");
        if (errors.Count == 0)
        {
            return false;
        }

        lastError = errors[0] as ErrorRecord;
        if (lastError is null && errors[0] is RuntimeException rtEx)
        {
            lastError = rtEx.ErrorRecord;
        }

        if (lastError?.InvocationInfo is null || lastError.InvocationInfo.HistoryId != lastHistory.Id)
        {
            return false;
        }

        return true;
    }
}
