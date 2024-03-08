using System;

namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// This class is used to execute Python code on the local machine. It inherits most functionality 
/// from the SubprocessLanguage class while implementing the PreprocessCode method for non-function calling
/// AIs.
/// </summary>
public class Python: SubprocessLanguage
{
	public Python()
	{
		StartCmd = ["python3.exe", "-"];
		OutputQueue = new Queue<Dictionary<string, string>>();
	}

	protected override string PreprocessCode(string code)
	{
		code += "\nprint('##end_of_execution##')";
        return code;
    }
}
