using System;
using System.Diagnostics;
using System.Text;

namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// Implements the IBaseLanguage interface with common process definitions. This is the parent class for all
/// languages.
/// </summary>
public abstract class SubprocessLanguage : IBaseLanguage
{
    private Process Process { get; set; }

    /// <summary>
    /// The command to start the process. This is an array of strings where the first element is the program
    /// to run and the second element is the arguments to pass to the program.
    /// </summary>
    protected string[] StartCmd { get; set; }
    protected Queue<Dictionary<string,string>> OutputQueue { get; set; }

    /// <summary>
    /// Preprocesses the code before running it removing backticks and language name.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    protected abstract string PreprocessCode(string code);

    /// <summary>
    /// Assigns process with a new process if possible.
    /// </summary>
    private void StartProcess()
    {
        if (Process != null)
        {
            Terminate();
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = StartCmd[0],
            Arguments = StartCmd[1],
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        Process = new Process { StartInfo = startInfo };
        Process.Start();

        Thread outputThread = new Thread(() =>
        {
            while (!Process.StandardOutput.EndOfStream)
            {
                StreamReader line = Process.StandardOutput;
                HandleStreamOutput(line, false);
            }
        });
        outputThread.Start();
        
        Thread errorThread = new Thread(() =>
        {
            while (!Process.StandardError.EndOfStream)
            {
                StreamReader line = Process.StandardError;
                HandleStreamOutput(line, true);
            }
        });
        errorThread.Start();
    }

    /// <summary>
    /// Runs the code and returns the output in a DataPacket.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public async Task<Queue<Dictionary<string, string>>> Run(string code)
    {
        string processedCode;
        try
        {
            processedCode = PreprocessCode(code);
            if(Process is null)
            {
                StartProcess();
            }
        }
        catch(Exception e)
        {
            OutputQueue.Enqueue(new Dictionary<string, string> {
                { "type", "error" },
                { "content", "Error starting process\n" + e }
            });
            return OutputQueue;
        }

        try
        {
            Process.StandardInput.WriteLine(processedCode + "\n");
            Process.StandardInput.Flush();
        }
        catch(Exception e)
        {
            OutputQueue.Enqueue(new Dictionary<string, string> {
                { "type", "error" },
                { "content", "Error writing to process\n" + e }
            });
            return OutputQueue;
        }

        while (true)
        {
            if(OutputQueue.Count > 0)
            {
                return OutputQueue;
            }
            else
            {
                await Task.Delay(100);
            }
            try
            {
                await Task.Delay(300);
                if(OutputQueue.Count > 0)
                {
                    return OutputQueue;
                }
            }
            catch(Exception e)
            {
                OutputQueue.Enqueue(new Dictionary<string, string>
                {
                    { "type", "error" },
                    { "content", "Error reading from process\n" + e }
                });
                return OutputQueue;
            }
        }
    }

    /// <summary>
    /// Ends the process and cleans up any resources.
    /// </summary>
    public void Terminate()
    {
        if(Process != null)
        {
            Process.StandardOutput.Close();
            Process.StandardError.Close();
            Process.Kill();
            Process.Dispose();
            Process = null;
        }
    }

    /// <summary>
    /// Internal function to handle the output of the process.
    /// </summary>
    private void HandleStreamOutput(StreamReader stream, bool isErrorStream)
    {
        string line;
        while ((line = stream.ReadLine()) != null)
        {
            if (isErrorStream)
            {
                OutputQueue.Enqueue(new Dictionary<string,string>
                {
                    { "type", "error" },
                    { "content", line },
                });
            }
            else
            {
                OutputQueue.Enqueue(new Dictionary<string,string>
                {
                    { "type", "output" },
                    { "content", line }
                });
            }
        }
    }

    /// <summary>
    /// Checks if pwsh.exe or python.exe in on System PATH. Returns false if not found.
    /// </summary>
    public bool IsOnPath()
    {
        var values = Environment.GetEnvironmentVariable("PATH");
        foreach (var path in values.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, StartCmd[0]);
            if (File.Exists(fullPath))
            {
                return true;
            }
        }
        return false;
    }
}
