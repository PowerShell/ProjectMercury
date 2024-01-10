using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using Spectre.Console;

namespace ShellCopilot.Kernel;

internal class Pager
{
    private static readonly string[] s_path;
    private static readonly string[] s_extension;

    static Pager()
    {
        string path = Environment.GetEnvironmentVariable("PATH");
        s_path = path.Split(Path.PathSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        s_extension = OperatingSystem.IsWindows() ? new[] { ".exe", ".com" } : Array.Empty<string>();
    }

    private const string DefaultPager = "less";
    private const string EnvVarName = "PAGER";

    private readonly bool _enabled;
    private readonly bool _specifiedByUser;
    private readonly string _command;
    private readonly string _arguments;

    internal Pager(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
        {
            (_command, _arguments) = GetPagerCommandAndArgs(out _specifiedByUser);
            _command = ResolveCommand(_command);
        }
    }

    internal void ReportAnyResolutionFailure()
    {
        if (_enabled && _command is null)
        {
            if (_specifiedByUser)
            {
                string inline = Formatter.InlineCode(EnvVarName);
                AnsiConsole.MarkupLine(Formatter.Error($"Command specified in the environment variable {inline} cannot be resolved."));
                AnsiConsole.MarkupLine(Formatter.Error("Paging functionality is disabled."));
                AnsiConsole.WriteLine();
            }
            else
            {
                string inline = Formatter.InlineCode(DefaultPager);
                AnsiConsole.MarkupLine(Formatter.Warning($"Default paging utility {inline} cannot be found in PATH. Paging functionality is disabled."));
                AnsiConsole.MarkupLine(Formatter.Warning($"It's recommended to enable paging when using the alternate screen buffer. Please consider install {inline} to your PATH"));
                AnsiConsole.WriteLine();
            }
        }
    }

    internal void WriteOutput(string text)
    {
        // Write out directly to the console if the pager is not enabled,
        // or if we failed to resolve the pager command.
        if (!_enabled || _command is null)
        {
            Console.WriteLine(text);
            return;
        }

        string oldTitle = OperatingSystem.IsWindows() ? Console.Title : null;
        ProcessStartInfo startInfo = new(_command, _arguments)
        {
            RedirectStandardInput = true,
            StandardInputEncoding = Encoding.Default,
            UseShellExecute = false,
        };

        // Add default options to `less` and `lv` when starting the pager, same as what `git` does.
        startInfo.Environment.TryAdd("LESS", "FRX");
        startInfo.Environment.TryAdd("LV", "-c");

        try
        {
            using var process = Process.Start(startInfo);
            process.StandardInput.WriteLine(text);
            process.StandardInput.Close();
            process.WaitForExit();
        }
        catch (Win32Exception e)
        {
            throw new ShellCopilotException(
                $"Failed to run the paging utility '{_command}': {e.Message}",
                innerException: e);
        }
        finally
        {
            if (oldTitle is not null)
            {
                Console.Title = oldTitle;
            }
        }
    }

    private static (string command, string args) GetPagerCommandAndArgs(out bool fromEnv)
    {
        string pager = Environment.GetEnvironmentVariable(EnvVarName)?.Trim();
        if (string.IsNullOrEmpty(pager))
        {
            fromEnv = false;
            return (command: DefaultPager, args: null);
        }

        fromEnv = true;
        if (pager.Contains(Path.DirectorySeparatorChar) && File.Exists(pager))
        {
            return (command: pager, args: null);
        }

        string command = null, args = null;
        if (pager.StartsWith('\'') || pager.StartsWith('"'))
        {
            int i = 1;
            char quote = pager[0];

            for (; i < pager.Length; i++)
            {
                if (pager[i] == quote)
                {
                    command = pager[1..i];
                    break;
                }
            }

            if (string.IsNullOrEmpty(command))
            {
                return (command: null, args: null);
            }

            int argStart = i + 1;
            if (pager.Length > argStart)
            {
                args = pager[argStart..].TrimStart();
            }

            return (command, args);
        }

        int spaceIndex = pager.IndexOf(' ');
        if (spaceIndex is -1)
        {
            return (command: pager, args: null);
        }

        command = pager[0..spaceIndex];
        args = pager[(spaceIndex + 1)..].TrimStart();
        return (command, args);
    }

    private static string ResolveCommand(string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            return null;
        }

        if (command.Contains(Path.DirectorySeparatorChar))
        {
            var file = new FileInfo(command);
            return IsExecutable(file) ? file.FullName : null;
        }

        var extSpan = Path.GetExtension(command.AsSpan());
        if (IsExecutableExtension(extSpan))
        {
            return SearchCommand(command);
        }

        foreach (string ext in s_extension)
        {
            string result = SearchCommand(command + ext);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static string SearchCommand(string command)
    {
        foreach (string entry in s_path)
        {
            string path = Path.Combine(entry, command);
            var file = new FileInfo(path);
            if (IsExecutable(file))
            {
                return file.FullName;
            }
        }

        if (command is "less.exe")
        {
            // Extra effort to find 'less.exe' on Windows if 'git' is installed.
            string gitPath = SearchCommand("git.exe");
            if (gitPath is not null && gitPath.EndsWith(@"Git\cmd\git.exe", StringComparison.OrdinalIgnoreCase))
            {
                string lessPath = gitPath.Replace(@"cmd\git.exe", @"usr\bin\less.exe", StringComparison.OrdinalIgnoreCase);
                if (File.Exists(lessPath))
                {
                    return lessPath;
                }
            }
        }

        return null;
    }

    private static bool IsExecutable(FileInfo file)
    {
        const UnixFileMode executeMode = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

        if (!file.Exists)
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return IsExecutableExtension(file.Extension);
        }

        return file.UnixFileMode.HasFlag(executeMode);
    }

    private static bool IsExecutableExtension(ReadOnlySpan<char> ext)
    {
        if (s_extension.Length is 0)
        {
            // The file extension doesn't matter on non-Windows.
            return true;
        }

        foreach (string entry in s_extension)
        {
            if (entry.AsSpan().Equals(ext, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
