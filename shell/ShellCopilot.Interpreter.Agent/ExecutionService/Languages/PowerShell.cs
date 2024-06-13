using AIShell.Abstraction;
using System.ComponentModel;
using System.Diagnostics;

namespace AIShell.Interpreter.Agent;

/// <summary>
/// This class is used to execute powershell code on the local machine. It inherits most functionality 
/// from the SubprocessLanguage class while implementing the PreprocessCode method for non-function calling
/// AIs.
/// </summary>
internal class PowerShell: SubprocessLanguage
{
    internal PowerShell() : base()
    {
        // -NoProfile prevents the profile from loading and -file - reads the code from stdin
        StartCmd = ["pwsh", "-NoProfile -Command -"];
        VersionCmd = ["pwsh", "--version"];
    }

    protected override string PreprocessCode(string code)
    {
        string try_catch_code = @$"
try {{
    $ErrorActionPreference = 'Stop'
    {code.AsSpan().TrimEnd()}
}}
catch {{
    $e = $_.Exception
    $msg = $e.GetType().FullName + "": "" + $e.Message
    $indent = """"
    while ($e.InnerException) {{
        $e = $e.InnerException
        $indent += ""---> ""
        $msg += ""`n"" + $indent + $e.GetType().FullName + "": "" + $e.Message
    }}
    [Console]::Error.WriteLine($msg)
    [Console]::Error.WriteLine($_.InvocationInfo.PositionMessage)
}} finally {{
    Write-Host '##end_of_execution##'
}}
";
        return try_catch_code;
    }
}
