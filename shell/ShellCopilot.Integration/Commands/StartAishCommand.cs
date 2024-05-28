using System.Diagnostics;
using System.Text.Json;
using System.Management.Automation;
using Microsoft.PowerShell.Commands;

namespace ShellCopilot.Integration;

[Alias("aish")]
[Cmdlet(VerbsLifecycle.Start, "Aish")]
public class StartAishCommand : PSCmdlet
{
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; }

    /// <summary>
    /// Cached GUID for the default profile of Windows Terminal.
    /// </summary>
    private static string s_wtDefaultProfileGuid;

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

        if (s_wtDefaultProfileGuid is null)
        {
            s_wtDefaultProfileGuid = string.Empty;
            string settingFile = System.IO.Path.Combine(
                Environment.GetEnvironmentVariable("LOCALAPPDATA"),
                "Packages",
                "Microsoft.WindowsTerminal_*",
                "LocalState",
                "settings.json");

            var matchingFiles = SessionState.Path.GetResolvedProviderPathFromProviderPath(settingFile, FileSystemProvider.ProviderName);
            if (matchingFiles.Count > 0)
            {
                using var stream = File.OpenRead(matchingFiles[0]);
                var jsonDoc = JsonDocument.Parse(stream);
                if (jsonDoc.RootElement.TryGetProperty("defaultProfile", out JsonElement value))
                {
                    s_wtDefaultProfileGuid = value.GetString();
                }
            }
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
                s_wtDefaultProfileGuid,
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
