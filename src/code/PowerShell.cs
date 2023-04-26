using System;
using System.Collections;
using System.Text;
using System.Management.Automation;

namespace Microsoft.PowerShell.Copilot
{
    internal class Pwsh
    {
        private static System.Management.Automation.PowerShell _pwsh = System.Management.Automation.PowerShell.Create();

        internal static string GetLastError(PSCmdlet cmdlet)
        {
            var errorVar = cmdlet.GetVariableValue("global:error");
            if (errorVar is ArrayList errorArray && errorArray.Count > 0)
            {
                _pwsh.Commands.Clear();
                _pwsh.AddCommand("Get-Error").AddParameter("InputObject", errorArray[0]);
                _pwsh.AddCommand("Out-String");
                var result = _pwsh.Invoke<string>();
                var sb = new StringBuilder();
                foreach (var item in result)
                {
                    sb.AppendLine(item);
                }

                return sb.ToString();
            }
            else
            {
                Screenbuffer.WriteConsole($"{PSStyle.Instance.Foreground.BrightMagenta}No error found.{PSStyle.Instance.Reset}\n");
            }

            return string.Empty;
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
