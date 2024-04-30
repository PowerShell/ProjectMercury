namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// This class is used to execute Python code on the local machine. It inherits most functionality 
/// from the SubprocessLanguage class while implementing the PreprocessCode method for non-function calling
/// AIs.
/// </summary>
internal class Python: SubprocessLanguage
{
    internal Python() : base()
    {
        // -q doesn't print the banner
        // -i runs the code in interactive mode
        // -u unbuffered binary stdout and stderr
        // Without these flags, the output is buffered and we can't read it until the process ends
        StartCmd = ["python", "-qui"];
        VersionCmd = ["python", "-V"];
    }

    protected override string PreprocessCode(string code)
    {
        // 1. Since code is inserted via string interpolation into an indented try block every subsequent line of
        // code needs an extra indent to be valid Python syntax.
        // 2. If code throws an error, the error message is printed to stderr for proper user prompt generation.
        // 3. Did not use the finally block to print `##end_of_execution##` because errors enountered
        // by Python parser are not caught by the try block.

        string try_catch_code =
$@"
import sys
import traceback
try:
    {code.Replace("\n", "\n    ")}
except Exception:
    print(traceback.format_exc(), file=sys.stderr)

print('##end_of_execution##')
";
        return try_catch_code;
    }

    protected override void WriteToProcess(string code)
    {
        Process.StandardInput.WriteLine(code);
        Process.StandardInput.Flush();
    }
}
