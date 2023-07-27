using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class ParentProcessUtilities
{
    internal struct ParentProcessInfo
    {
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2_0;
        internal IntPtr Reserved2_1;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessInfo processInformation, int processInformationLength, out int returnLength);

    private static bool IsRunningOnWindows()
    {
        int platform = (int)Environment.OSVersion.Platform;
        return platform == 2 || platform == 3;
    }

    public static Process? GetParentProcess()
    {
        if (IsRunningOnWindows())
        {
            if (IsRunningInPowerShell())
            {
                Process process = Process.GetCurrentProcess();
                string query = $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}";
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"({query}).ParentProcessId\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process? pwsh = Process.Start(psi);
                string? output = pwsh?.StandardOutput.ReadToEnd();
                pwsh?.WaitForExit();
                int parentProcessId;
                if (int.TryParse(output?.Trim(), out parentProcessId))
                {
                    try
                    {
                        return Process.GetProcessById(parentProcessId);
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                }
            }
            else
            {
                return GetParentProcessOnWindows(Process.GetCurrentProcess().Handle);
            }
        }
        else
        {
            return GetParentProcessOnUnix();
        }

        return null;
    }

    private static bool IsRunningInPowerShell()
    {
        IntPtr handle = GetStdHandle(-10); 
        int consoleMode;
        GetConsoleMode(handle, out consoleMode);

        return consoleMode == 256;
    }

    private static Process? GetParentProcessOnWindows(IntPtr handle)
    {
        ParentProcessInfo pbi = new ParentProcessInfo();
        int returnLength;
        int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
        if (status != 0)
            throw new Win32Exception(status);

        try
        {
            return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Process? GetParentProcessOnUnix()
    {
        Process process = Process.GetCurrentProcess();
        string? output = RunCommand($"ps -o ppid= {process.Id}");
        int parentProcessId;
        if (int.TryParse(output?.Trim(), out parentProcessId))
        {
            try
            {
                return Process.GetProcessById(parentProcessId);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return null;
    }

    private static string? RunCommand(string command)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process = Process.Start(psi);
        string? output = process?.StandardOutput.ReadToEnd();
        process?.WaitForExit();
        return output;
    }
}
