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
        StartCmd = ["pwsh.exe", "-file - -NoProfile"];
        OutputQueue = new Queue<Dictionary<string, string>>();
    }

    protected override string PreprocessCode(string code)
    {
        code = code.TrimEnd();
        // Add end marker (listen for this in HandleStreamOutput to know when code ends)
        code += "\nWrite-Output '##end_of_execution##'";
        return code;
    }

    protected override void WriteToProcess(string code)
    {
        Process.StandardInput.WriteLine(code);
        Process.StandardInput.Flush();
    }
}
