using System;

namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// This class is used to execute powershell code on the local machine. It inherits most functionality 
/// from the SubprocessLanguage class while implementing the PreprocessCode method for non-function calling
/// AIs.
/// </summary>
public class PowerShell: SubprocessLanguage
{
    public PowerShell()
    {
        StartCmd = ["pwsh.exe", "-file -"];
        OutputQueue = new Queue<Dictionary<string, string>>();
    }

    protected override string PreprocessCode(string code)
    {
        return code;
    }
}
