using System.Diagnostics;
using System.Management.Automation;
using System.Text;

namespace AIShell.Integration.Commands;

[Alias("aish")]
[Cmdlet(VerbsLifecycle.Start, "AIShell")]
public class StartAIShellCommand : PSCmdlet
{
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; }

    protected override void BeginProcessing()
    {
        if (Path is null)
        {
            var app = SessionState.InvokeCommand.GetCommand("aish", CommandTypes.Application);
            if (app is null)
            {
                ThrowTerminatingError(new(
                    new NotSupportedException("The executable 'aish' cannot be found."),
                    "AIShellMissing",
                    ErrorCategory.NotInstalled,
                    targetObject: null));
            }

            Path = app.Source;
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

        if (OperatingSystem.IsWindows())
        {
            // Validate if Windows Terminal is installed.
            var wtExe = SessionState.InvokeCommand.GetCommand("wt", CommandTypes.Application);
            if (wtExe is null)
            {
                ThrowTerminatingError(new(
                    new NotSupportedException("The executable 'wt' (Windows Terminal) cannot be found."),
                    "WindowsTerminalMissing",
                    ErrorCategory.NotInstalled,
                    targetObject: null));
            }

            // Validate if Windows Terminal is running, and assuming we are running in WT if the process exists.
            Process[] ps = Process.GetProcessesByName("WindowsTerminal");
            if (ps.Length is 0)
            {
                ThrowTerminatingError(new(
                    new NotSupportedException("The 'WindowsTerminal' process is not found. Please make sure running this cmdlet from within Windows Terminal."),
                    "NotInWindowsTerminal",
                    ErrorCategory.InvalidOperation,
                    targetObject: null));
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            string term = Environment.GetEnvironmentVariable("TERM_PROGRAM");
            if (!string.Equals(term, "iTerm.app", StringComparison.Ordinal))
            {
                ThrowTerminatingError(new(
                    new NotSupportedException("The environment variable 'TERM_PROGRAM' is missing or its value is not 'iTerm.app'. Please make sure running this cmdlet from within iTerm2."),
                    "NotIniTerm2",
                    ErrorCategory.InvalidOperation,
                    targetObject: null));
            }

            var python = SessionState.InvokeCommand.GetCommand("python3", CommandTypes.Application);
            if (python is null)
            {
                ThrowTerminatingError(new(
                    new NotSupportedException("The executable 'python3' (Windows Terminal) cannot be found. It's required to split a pane in iTerm2 programmatically."),
                    "Python3Missing",
                    ErrorCategory.NotInstalled,
                    targetObject: null));
            }

            var pip3 = SessionState.InvokeCommand.GetCommand("pip3", CommandTypes.Application);
            if (pip3 is null)
            {
                ThrowTerminatingError(new(
                    new NotSupportedException("The executable 'pip3' cannot be found. It's required to split a pane in iTerm2 programmatically."),
                    "Pip3Missing",
                    ErrorCategory.NotInstalled,
                    targetObject: null));
            }
        }
        else
        {
            ThrowTerminatingError(new(
                new NotSupportedException("This platform is not yet supported."),
                "PlatformNotSupported",
                ErrorCategory.NotEnabled,
                targetObject: null));
        }
    }

    protected override void EndProcessing()
    {
        string pipeName = Channel.Singleton.StartChannelSetup();

        if (OperatingSystem.IsWindows())
        {
            ProcessStartInfo startInfo;
            string wtProfileGuid = Environment.GetEnvironmentVariable("WT_PROFILE_ID");

            if (wtProfileGuid is null)
            {
                // We may be running in a WT that was started by OS as the default terminal.
                // In this case, we don't specify the '-p' option.
                startInfo = new("wt")
                {
                    ArgumentList = {
                        "-w",
                        "0",
                        "sp",
                        "--tabColor",
                        "#345beb",
                        "-s",
                        "0.4",
                        "--title",
                        "AIShell",
                        Path,
                        "--channel",
                        pipeName
                    },
                };
            }
            else
            {
                // Specify the '-p' option to use the same profile.
                startInfo = new("wt")
                {
                    ArgumentList = {
                        "-w",
                        "0",
                        "sp",
                        "--tabColor",
                        "#345beb",
                        "-p",
                        wtProfileGuid,
                        "-s",
                        "0.4",
                        "--title",
                        "AIShell",
                        Path,
                        "--channel",
                        pipeName
                    },
                };
            }

            Process.Start(startInfo);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // Install the Python package 'iterm2'.
            ProcessStartInfo startInfo = new("pip3")
            {
                ArgumentList = { "install", "-q", "iterm2" },
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            Process proc = new() { StartInfo = startInfo };
            proc.Start();
            proc.WaitForExit();

            if (proc.ExitCode is 1)
            {
                ThrowTerminatingError(new(
                    new NotSupportedException("The Python package 'iterm2' cannot be installed. It's required to split a pane in iTerm2 programmatically."),
                    "iterm2Missing",
                    ErrorCategory.NotInstalled,
                    targetObject: null));
            }

            proc.Dispose();

            // Write the Python script to a temp file, if not yet.
            string pythonScript = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "__aish_split_pane.py");
            if (!File.Exists(pythonScript))
            {
                File.WriteAllText(pythonScript, SplitPanePythonCode, Encoding.UTF8);
            }

            // Run the Python script to split the pane and start AIShell.
            startInfo = new("python3") { ArgumentList = { pythonScript, Path, pipeName } };
            proc = new() { StartInfo = startInfo };
            proc.Start();
            proc.WaitForExit();
        }
    }

    private const string SplitPanePythonCode = """
        import iterm2
        import sys

        # iTerm needs to be running for this to work
        async def main(connection):
            app = await iterm2.async_get_app(connection)

            # Foreground the app
            await app.async_activate()

            window = app.current_terminal_window
            if window is not None:
                # Get the current pane so that we can split it.
                current_tab = window.current_tab
                current_pane = current_tab.current_session

                # Get the total width before splitting.
                width = current_pane.grid_size.width

                # Split pane vertically
                split_pane = await current_pane.async_split_pane(vertical=True)

                # Get the height of the pane after splitting. This value will be
                # slightly smaller than its height before splitting.
                height = current_pane.grid_size.height

                # Calculate the new width for both panes using the ratio 0.4 for the new pane.
                # Then set the preferred size for both pane sessions.
                new_current_width = round(width * 0.6);
                new_split_width = width - new_current_width;
                current_pane.preferred_size = iterm2.Size(new_current_width, height)
                split_pane.preferred_size = iterm2.Size(new_split_width, height);

                # Update the layout, which will change the panes to preferred size.
                await current_tab.async_update_layout()

                await split_pane.async_send_text(f'{app_path} --channel {channel}\n')
            else:
                # You can view this message in the script console.
                print("No current iTerm2 window. Make sure you are running in iTerm2.")

        if len(sys.argv) > 1:
            app_path = sys.argv[1]
            channel = sys.argv[2]

            # Do not specify True for retry. It's possible that the user hasn't enable the Python API for iTerm2,
            # and in that case, we want it to fail immediately instead of stucking in retries.
            iterm2.run_until_complete(main)
        else:
            print("Please provide the application path as a command line argument.")
        """;
}
