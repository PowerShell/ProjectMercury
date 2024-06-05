using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.Sockets;

namespace ShellCopilot.Ollama.Agent;

internal static class Utils
{
    /// <summary>
    /// Confirms a given CLI tool is installed on the system
    /// </summary>
    /// <param name="toolName">CLI tools name to check</param>
    /// <returns>Boolean whether or not the CLI tool is installed</returns>
    public static bool IsCliToolInstalled(string toolName)
    {
        string shellCommand, shellArgument;
        // Determine the shell command and arguments based on the OS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            shellCommand = "cmd.exe";
            shellArgument = "/c " + toolName + " --version";
        }
        else
        {
            shellCommand = "/bin/bash";
            shellArgument = "-c \"" + toolName + " --version\"";
        }

        try
        {
            ProcessStartInfo procStartInfo = new ProcessStartInfo(shellCommand, shellArgument)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = procStartInfo;
                process.Start();

                // You can read the output or error if necessary for further processing
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// Confirms a localhost port is open to ensure ollama server is running
    /// </summary>
    /// <param name="port">port number to check against</param>
    /// <returns>Boolean whether or not the localhost port is responding</returns>
    public static bool IsPortResponding(int port)
    {
        using (TcpClient tcpClient = new TcpClient())
        {
            try
            {
                // Attempt to connect to the specified port on localhost
                tcpClient.Connect("localhost", port);
                return true;
            }
            catch (SocketException ex)
            {
                return false; 
            }
        }
    }

}