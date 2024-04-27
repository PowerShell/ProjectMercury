namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// This class is used to execute Python code on the local machine. It inherits most functionality 
/// from the SubprocessLanguage class while implementing the PreprocessCode method for non-function calling
/// AIs.
/// </summary>
internal class Python: SubprocessLanguage
{
    internal Python()
    {
        // -q doesn't print the banner
        // -i runs the code in interactive mode
        // -u unbuffered binary stdout and stderr
        // Without these flags, the output is buffered and we can't read it until the process ends
        StartCmd = ["python.exe", "-qui"];
        VersionCmd = ["python.exe", "-V"];
        OutputQueue = new();
    }

    protected override string PreprocessCode(string code)
    {
        code = code.TrimEnd();
        code += "\n\nprint('##end_of_execution##')";
        return code;
    }

    protected override void WriteToProcess(string code)
    {
        // Split the code into lines and send each line to the process
        var codeSpan = code.AsSpan();

        // Count all '\n' in the code
        int numLines = code.Count(c => c == '\n');

        // Create a span to hold the ranges of each line
        Range[] numLinesRange = new Range[numLines];

        // Initialize the size of the Span with the number of lines
        var lines = new Span<Range>(numLinesRange);

        // Split the code into lines
        int lineNums = MemoryExtensions.Split(codeSpan, lines, '\n');
        
        foreach(Range line in lines)
        {
            ReadOnlySpan<char> lineSpan = codeSpan.Slice(line.Start.Value, line.End.Value - line.Start.Value);

            Process.StandardInput.WriteLine(lineSpan.ToString());
            Process.StandardInput.Flush();
        }
        
        // List<string> lines = code.Split("\n").ToList();

        // foreach (string line in lines)
        // {
        //     Process.StandardInput.WriteLine(line);
        //     Process.StandardInput.Flush();
        // }

    }
}
