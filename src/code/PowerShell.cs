using System;
using System.Text;
using System.Management.Automation;

namespace Microsoft.PowerShell.Copilot
{
    internal class Pwsh
    {
        private static System.Management.Automation.PowerShell _pwsh = System.Management.Automation.PowerShell.Create();

        internal static string GetLastError()
        {
            _pwsh.Commands.Clear();
            _pwsh.AddCommand("Get-Error");
            _pwsh.AddCommand("Out-String");
            var result = _pwsh.Invoke<string>();
            var sb = new StringBuilder();
            foreach (var item in result)
            {
                sb.AppendLine(item);
            }

            var error = sb.ToString();

            if (string.IsNullOrEmpty(error))
            {
                Screenbuffer.WriteConsole($"{PSStyle.Instance.Foreground.BrightMagenta}No error found.{Screenbuffer.RESET}\n");
            }

            return error;
        }

        internal static void CopyToClipboard(string input)
        {
            _pwsh.Commands.Clear();
            _pwsh.AddCommand("Set-Clipboard");
            _pwsh.AddParameter("Value", input);
            _pwsh.Invoke();
        }

        internal static ConsoleKeyInfo GetPSReadLineKeyHandler()
        {
            var key = "F3";
            var script = @"(Get-PSReadLineKeyHandler -Bound | Where-Object { $_.Description.StartsWith('PSCopilot:') }).Key";
            _pwsh.Commands.Clear();
            _pwsh.AddScript(script);
            var result = _pwsh.Invoke<string>();
            if (result.Count > 0 && result[0].Length > 0)
            {
                key = result[0];
            }

            return new ConsoleKeyInfo('\0', (ConsoleKey)Enum.Parse(typeof(ConsoleKey), key), shift: false, alt: false, control: false);
        }
    }
}
