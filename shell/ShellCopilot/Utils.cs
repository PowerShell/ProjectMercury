using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ShellCopilot;

internal static class Utils
{
    internal const int InvalidProcessId = -1;
    internal const string AppName = "ai";

    internal static readonly string OS;
    internal static readonly string AppConfigHome;

    private static int? s_parentProcessId;

    static Utils()
    {
        string rid = RuntimeInformation.RuntimeIdentifier;
        int index = rid.IndexOf('-');
        OS = index is -1 ? rid : rid.Substring(0, index);

        bool isWindows = OperatingSystem.IsWindows();
        string locationPath = isWindows
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("HOME");
        AppConfigHome = Path.Combine(locationPath, AppName);

        if (!Directory.Exists(AppConfigHome))
        {
            Directory.CreateDirectory(AppConfigHome);
            if (isWindows)
            {
                SetDirectoryACLs(AppConfigHome);
            }
            else
            {
                SetFilePermissions(AppConfigHome, isDirectory: true);
            }
        }
    }

    internal static string GetDataFromSecureString(SecureString secureString)
    {
        if (secureString is null || secureString.Length is 0)
        {
            return null;
        }

        nint ptr = Marshal.SecureStringToBSTR(secureString);
        try
        {
            return Marshal.PtrToStringBSTR(ptr);
        }
        finally
        {
            Marshal.ZeroFreeBSTR(ptr);
        }
    }

    internal static SecureString ConvertDataToSecureString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var ss = new SecureString();
        foreach (char c in text)
        {
            ss.AppendChar(c);
        }

        return ss;
    }

    internal static void SetDirectoryACLs(string directoryPath)
    {
        Debug.Assert(OperatingSystem.IsWindows());

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
        System.IO.FileSystemAclExtensions.SetAccessControl(
            directoryInfo: new DirectoryInfo(directoryPath),
            directorySecurity: dirSecurity);
    }

    internal static void SetFilePermissions(string path, bool isDirectory)
    {
        // Non-Windows platforms.

        // Set directory permissions to current user only.
        /*
        Current user is user owner.
        Current user is group owner.
        Permission for user dir owner:      rwx    (execute for directories only)
        Permission for user file owner:     rw-    (no file execute)
        Permissions for group owner:        ---    (no access)
        Permissions for others:             ---    (no access)
        */
        string argument = isDirectory ? 
            string.Format(CultureInfo.InvariantCulture, @"u=rwx,g=---,o=--- {0}", path) :
            string.Format(CultureInfo.InvariantCulture, @"u=rw-,g=---,o=--- {0}", path);

        ProcessStartInfo startInfo = new("chmod", argument);
        Process.Start(startInfo).WaitForExit();
    }

    internal static int GetParentProcessId()
    {
        if (!s_parentProcessId.HasValue)
        {
            s_parentProcessId = GetParentProcessId(Process.GetCurrentProcess());
        }

        return s_parentProcessId.Value;
    }

    private static int GetParentProcessId(Process process)
    {
        if (OperatingSystem.IsWindows())
        {
            var res = Interop.Windows.NtQueryInformationProcess(
                process.Handle,
                processInformationClass: 0,
                processInformation: out Interop.Windows.PROCESS_BASIC_INFORMATION pbi,
                processInformationLength: Marshal.SizeOf<Interop.Windows.PROCESS_BASIC_INFORMATION>(),
                returnLength: out int size);

            return res is 0 ? pbi.InheritedFromUniqueProcessId.ToInt32() : InvalidProcessId;
        }
        else if (OperatingSystem.IsMacOS())
        {
            return Interop.MacOS.GetPPid(process.Id);
        }
        else if (OperatingSystem.IsLinux())
        {
            // Read '/proc/<pid>/status' for the row beginning with 'PPid:', which is the parent process id.
            // We could check '/proc/<pid>/stat', but although that file was meant to be a space delimited line,
            // it contains a value which could contain spaces itself.
            // Using the 'status' file is a lot simpler because each line contains a record with a simple label.
            // https://github.com/PowerShell/PowerShell/issues/17541#issuecomment-1159911577
            var path = $"/proc/{process.Id}/status";
            try
            {
                string line = null;
                using StreamReader sr = new(path);

                while ((line = sr.ReadLine()) is not null)
                {
                    if (!line.StartsWith("PPid:\t", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] lineSplit = line.Split(
                        separator: '\t',
                        count: 2,
                        options: StringSplitOptions.RemoveEmptyEntries);

                    if (lineSplit.Length is not 2)
                    {
                        continue;
                    }

                    if (int.TryParse(lineSplit[1].Trim(), out int ppid))
                    {
                        return ppid;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore exception thrown from reading the proc file.
            }
        }

        return InvalidProcessId;
    }
}

internal static partial class Interop
{
    internal static unsafe partial class Windows
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            public nint ExitStatus;
            public nint PebBaseAddress;
            public nint AffinityMask;
            public nint BasePriority;
            public nint UniqueProcessId;
            public nint InheritedFromUniqueProcessId;
        }

        [LibraryImport("ntdll.dll")]
        internal static partial int NtQueryInformationProcess(
                nint processHandle,
                int processInformationClass,
                out PROCESS_BASIC_INFORMATION processInformation,
                int processInformationLength,
                out int returnLength);
    }

    internal static unsafe partial class MacOS
    {
        [LibraryImport("libpsl-native")]
        internal static partial int GetPPid(int pid);
    }
}
