using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

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
    internal const int InvalidProcessId = -1;
    internal const string AppName = "aish";

    internal static readonly string OS;
    internal static readonly string ShellConfigHome;
    internal static readonly string AgentHome;
    internal static readonly string AgentConfigHome;

    static Utils()
    {
        string rid = RuntimeInformation.RuntimeIdentifier;
        int index = rid.IndexOf('-');
        OS = index is -1 ? rid : rid[..index];

        string locationPath = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("HOME");

        ShellConfigHome = Path.Combine(locationPath, AppName);
        AgentHome = Path.Join(ShellConfigHome, "agents");
        AgentConfigHome = Path.Join(ShellConfigHome, "agent-config");

        // Create the folders if they don't exist.
        CreateFolderWithRightPermission(ShellConfigHome);
        Directory.CreateDirectory(AgentHome);
        Directory.CreateDirectory(AgentConfigHome);
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
