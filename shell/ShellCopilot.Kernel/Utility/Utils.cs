using System.Diagnostics;
using System.Globalization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerShell;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel;

internal sealed class Disposable : IDisposable
{
    private Action m_onDispose;

    internal static readonly Disposable NonOp = new();

    private Disposable()
    {
        m_onDispose = null;
    }

    public Disposable(Action onDispose)
    {
        m_onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    public void Dispose()
    {
        if (m_onDispose != null)
        {
            m_onDispose();
            m_onDispose = null;
        }
    }
}

internal static class Utils
{
    internal const string DefaultAppName = "aish";
    internal const string DefaultPrompt = "Copilot";

    internal static string AppName;
    internal static string ShellConfigHome;
    internal static string AgentHome;
    internal static string AgentConfigHome;

    internal static void Setup(string appName)
    {
        string locationPath = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("HOME");

        AppName = appName?.Trim().ToLower() ?? DefaultAppName;
        ShellConfigHome = Path.Combine(locationPath, AppName.Replace(' ', '.'));
        AgentHome = Path.Join(ShellConfigHome, "agents");
        AgentConfigHome = Path.Join(ShellConfigHome, "agent-config");

        // Create the folders if they don't exist.
        CreateFolderWithRightPermission(ShellConfigHome);
        Directory.CreateDirectory(AgentHome);
        Directory.CreateDirectory(AgentConfigHome);
    }

    internal static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
    }

    /// <summary>
    /// Check if the <paramref name="left"/> contains <paramref name="right"/> regardless of
    /// the leading and trailing space characters on each line if both are multi-line strings.
    /// </summary>
    internal static bool Contains(string left, string right)
    {
        ReadOnlySpan<char> leftSpan = left.AsSpan();
        if (leftSpan.IndexOf('\n') is -1)
        {
            // The 'left' is not a multi-line string, then a direct 'Contains' call is enough.
            return leftSpan.Contains(right.AsSpan().Trim(), StringComparison.Ordinal);
        }

        // The 'left' is a multi-line string. If the 'right' is also a multi-line string,
        // we want to check line by line regardless the leading and trailing space chars
        // on each line.
        int start, index = -1;
        while (true)
        {
            start = index + 1;
            if (start == right.Length)
            {
                break;
            }

            index = right.IndexOf('\n', start);
            if (index is -1)
            {
                return leftSpan.Contains(right.AsSpan(start).Trim(), StringComparison.Ordinal);
            }

            if (!leftSpan.Contains(right.AsSpan(start, index - start).Trim(), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Extract code blocks from the passed-in text.
    /// </summary>
    internal static List<CodeBlock> ExtractCodeBlocks(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        int start, index = -1;
        bool inCodeBlock = false;
        string language = null;
        StringBuilder code = null;
        List<CodeBlock> codeBlocks = null;

        do
        {
            start = index + 1;
            if (start == text.Length)
            {
                // Break out if we already reached end of the text.
                break;
            }

            index = text.IndexOf('\n', start);
            ReadOnlySpan<char> line = index is -1
                ? text.AsSpan(start)
                : text.AsSpan(start, index - start + 1);

            // Trim the line before checking for code fence.
            ReadOnlySpan<char> lineTrimmed = line.Trim();
            if (lineTrimmed.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    if (lineTrimmed.Length is 3)
                    {
                        // Current line is the ending code fence.
                        codeBlocks.Add(new CodeBlock(code.ToString(), language));

                        code.Clear();
                        language = null;
                        inCodeBlock = false;
                        continue;
                    }

                    // It's not the ending code fence, so keep appending to code.
                    code.Append(line);
                }
                else
                {
                    // Current line is the starting code fence.
                    code ??= new StringBuilder();
                    codeBlocks ??= [];
                    inCodeBlock = true;
                    language = lineTrimmed.Length > 3 ? lineTrimmed[3..].ToString() : null;
                }

                continue;
            }

            if (inCodeBlock)
            {
                // Append the line when we are within a code block.
                code.Append(line);
            }
        }
        while (index is not -1);

        if (inCodeBlock && code.Length > 0)
        {
            // It's possbile that the ending code fence is missing.
            codeBlocks.Add(new CodeBlock(code.ToString(), language));
        }

        return codeBlocks;
    }

    internal static void SetDefaultKeyHandlers()
    {
        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+d,Ctrl+c" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeAll",
            "Copy all the code snippets from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+1" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 1");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeOne",
            "Copy the 1st code snippet from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+2" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 2");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeTwo",
            "Copy the 2nd code snippet from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+3" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 3");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeThree",
            "Copy the 3rd code snippet from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+4" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 4");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeFour",
            "Copy the 4th code snippet from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+5" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 5");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeFive",
            "Copy the 5th code snippet from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+6" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 6");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeSix",
            "Copy the 6th code snippet from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+7" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 7");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeSeven",
            "Copy the 7th code snippet from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+8" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 8");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeEight",
            "Copy the 8th code snippet from the last response to clipboard.");

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+9" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy 9");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCodeNine",
            "Copy the 9th code snippet from the last response to clipboard.");
    }

    private static void CreateFolderWithRightPermission(string dirPath)
    {
        if (Directory.Exists(dirPath))
        {
            return;
        }

        Directory.CreateDirectory(dirPath);
        if (OperatingSystem.IsWindows())
        {
            // Windows platform.
            // For Windows, file permissions are set to FullAccess for current user account only.
            // SetAccessRule method applies to this directory.
            var dirSecurity = new DirectorySecurity();
            dirSecurity.SetAccessRule(
                new FileSystemAccessRule(
                    identity: WindowsIdentity.GetCurrent().User,
                    type: AccessControlType.Allow,
                    fileSystemRights: FileSystemRights.FullControl,
                    inheritanceFlags: InheritanceFlags.None,
                    propagationFlags: PropagationFlags.None));

            // AddAccessRule method applies to child directories and files.
            dirSecurity.AddAccessRule(
                new FileSystemAccessRule(
                identity: WindowsIdentity.GetCurrent().User,
                fileSystemRights: FileSystemRights.FullControl,
                type: AccessControlType.Allow,
                inheritanceFlags: InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                propagationFlags: PropagationFlags.InheritOnly));

            // Set access rule protections.
            dirSecurity.SetAccessRuleProtection(
                isProtected: true,
                preserveInheritance: false);

            // Set directory owner.
            dirSecurity.SetOwner(WindowsIdentity.GetCurrent().User);

            // Apply new rules.
            FileSystemAclExtensions.SetAccessControl(
                directoryInfo: new DirectoryInfo(dirPath),
                directorySecurity: dirSecurity);
        }
        else
        {
            // On non-Windows platforms, set directory permissions to current user only.
            //   Current user is user owner.
            //   Current user is group owner.
            //   Permission for user dir owner:      rwx    (execute for directories only)
            //   Permission for user file owner:     rw-    (no file execute)
            //   Permissions for group owner:        ---    (no access)
            //   Permissions for others:             ---    (no access)
            string argument = string.Format(CultureInfo.InvariantCulture, @"u=rwx,g=---,o=--- {0}", dirPath);
            ProcessStartInfo startInfo = new("chmod", argument);
            Process.Start(startInfo).WaitForExit();
        }
    }
}
