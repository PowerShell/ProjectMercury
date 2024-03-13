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
    protected Process Process { get; set; }

    /// <summary>
    /// The command to start the process. This is an array of strings where the first element is the program
    /// to run and the second element is the arguments to pass to the program.
    /// </summary>
    protected string[] StartCmd { get; set; }

    /// <summary>
    /// This event is used to signal when the process has finished running.
    /// </summary>

    protected ManualResetEvent Done = new ManualResetEvent(false);

    /// <summary>
    /// The queue to store the output of code processes.
    /// </summary>
    protected Queue<Dictionary<string,string>> OutputQueue { get; set; }

    /// <summary>
    /// Preprocesses the code before running it removing backticks and language name.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    protected abstract string PreprocessCode(string code);

    protected abstract void WriteToProcess(string input);

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
            StandardErrorEncoding = Encoding.UTF8,
        };

        Process = new Process { StartInfo = startInfo };

        Process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                HandleStreamOutput(e.Data, false);
            }
        });
        Process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                HandleStreamOutput(e.Data, true);
            }
        });

        Process.Start();

        Process.BeginOutputReadLine();
        Process.BeginErrorReadLine();
    }

    /// <summary>
    /// Runs the code and returns the output in a DataPacket.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public async Task<Queue<Dictionary<string, string>>> Run(string code, CancellationToken token)
    {
        OutputQueue.Clear();

        try
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

            // Reset the event so we can wait for the process to finish
            Done.Reset();

            try
            {
                WriteToProcess(processedCode);
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
                if(OutputQueue.Count > 0 && Done.WaitOne(0))
                {
                    await Task.Delay(1000);
                    return OutputQueue;
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    /// <summary>
    /// Ends the process and cleans up any resources.
    /// </summary>
    public void Terminate()
    {
        if(Process != null)
        {
            Done.Set();
            Process.Kill();
            Process.Dispose();
            Process = null;
        }
    }

    /// <summary>
    /// Internal function to handle the output of the process.
    /// </summary>
    private void HandleStreamOutput(string line, bool isErrorStream)
    {
        if (isErrorStream)
        {
            OutputQueue.Enqueue(new Dictionary<string,string>
            {
                { "type", "error" },
                { "content", line },
            });
            Done.Set();
        }
        else
        {
            if(DetectEndOfExecution(line))
            {
                OutputQueue.Enqueue(new Dictionary<string,string>
                {
                    { "type", "end" },
                    { "content", line }
                });
                Done.Set();
                return;
            }
            OutputQueue.Enqueue(new Dictionary<string,string>
            {
                { "type", "output" },
                { "content", line }
            });
        }
    }
    protected bool DetectEndOfExecution(string line)
    {
        return line.Contains("##end_of_execution##");
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
