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
		// -q doesn't print the banner
		// -i runs the code in interactive mode
		// -u unbuffered binary stdout and stderr
		// Without these flags, the output is buffered and we can't read it until the process ends
		StartCmd = ["python.exe", "-qui"];
		OutputQueue = new Queue<Dictionary<string, string>>();
	}

	protected override string PreprocessCode(string code)
	{
		code += "\nprint('##end_of_execution##')";
        return code;
    }

	protected override void WriteToProcess(string code)
	{
		// Split the code into lines and send each line to the process
		List<string> lines = code.Split("\n").ToList();

		foreach (string line in lines)
		{
            Process.StandardInput.WriteLine(line);
            Process.StandardInput.Flush();
        }
    }
}
