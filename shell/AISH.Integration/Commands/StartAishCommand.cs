using System.Diagnostics;
using System.Management.Automation;

namespace AISH.Integration;

[Alias("aish")]
[Cmdlet(VerbsLifecycle.Start, "Aish")]
public class StartAishCommand : PSCmdlet
{
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; }

    protected override void BeginProcessing()
    {
        if (Path is null)
        {
            Path = "aish";
            if (SessionState.InvokeCommand.GetCommand(Path, CommandTypes.Application) is null)
            {
                ThrowTerminatingError(new(
                    new NotSupportedException("The executable 'aish' cannot be found."),
                    "AISHMissing",
                    ErrorCategory.NotInstalled,
                    targetObject: null));
            }
        }
        else
        {
            var paths = GetResolvedProviderPathFromPSPath(Path, out _);
            if (paths.Count > 1)
            {
                ThrowTerminatingError(new(
                    new ArgumentException("Specified path is ambiguous as it's resolved to more than one paths."),
                    "InvalidPath",
                    ErrorCategory.InvalidArgument,
                    targetObject: null
                ));
            }

            Path = paths[0];
        }

        var wtExe = SessionState.InvokeCommand.GetCommand("wt", CommandTypes.Application);
        if (wtExe is null)
        {
            ThrowTerminatingError(new(
                new NotSupportedException("The executable 'wt' (Windows Terminal) cannot be found."),
                "AISHMissing",
                ErrorCategory.NotInstalled,
                targetObject: null));
        }
    }

    protected override void EndProcessing()
    {
        string pipeName = AishChannel.Singleton.StartChannelSetup();
        ProcessStartInfo startInfo = new("wt")
        {
            ArgumentList = {
                "-w",
                "0",
                "sp",
                "--tabColor",
                "#345beb",
                "-p",
                "{574e775e-4f2a-5b96-ac1e-a2962a402336}",
                "-s",
                "0.4",
                "--title",
                "AISH",
                Path,
                "--channel",
                pipeName
            },
        };

        Process.Start(startInfo);
    }
}
