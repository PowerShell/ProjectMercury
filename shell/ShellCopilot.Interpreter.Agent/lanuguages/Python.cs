using System;
using System.Linq;
using System.Diagnostics;
using ShellCopilot.Abstraction;
using System.Security.Claims;
using System.ComponentModel;
using System.Text;

public class Python
{
    private string _code { get; set; }
    private string[] _error { get; set; }
    private string[] _output { get; set; }
    private int _languageLength = "python\n".Length;
    private string tempFile;
    private int _maxOutputLength = 500;

    public Python()
    {
        tempFile = System.IO.Path.GetTempFileName();
    }

    public void PreprocessCode(string code)
    {
        _code = code.Remove(0,_languageLength);
    }

    public async Task<string[]> Run()
    {
        if (!IsPythonInstalled())
        {
            _error = new string[2];
            _error[0] = "error";
            _error[1] = "Python was not found on path.";
            return _error;
        }
        else
        { 
            System.IO.File.AppendAllText(tempFile, _code);
            // execute the file
            ProcessStartInfo startInfo = new()
            {
                FileName = "python", // Assumes 'python.exe' is in the system PATH
                Arguments = tempFile, // Execute the Python code
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using Process process = Process.Start(startInfo);
            StreamReader streamReader = process.StandardOutput;
            StreamReader errorReader = process.StandardError;

            StringBuilder outputBuilder = new();
            StringBuilder errorBuilder = new();

            // Read the output and error streams asynchronously
            Task<string> outputTask = ReadStreamAsync(streamReader, outputBuilder);
            Task<string> errorTask = ReadStreamAsync(errorReader, errorBuilder);

            // Wait for both tasks to complete
            await Task.WhenAll(outputTask, errorTask);

            // Get the output and error strings
            string output = outputTask.Result;
            string error = errorTask.Result;

            // Clean up resources
            streamReader.Close();
            errorReader.Close();

            await process.WaitForExitAsync();
            _error = new string[2];
            _error[0] = "error";
            _error[1] = error;

            if (process.ExitCode == 0)
            {
                if (!_error[1].Contains("error", StringComparison.CurrentCultureIgnoreCase))
                {
                    _output = new string[2];
                    _output[0] = "output";
                    if (_error[1].Contains("warning", StringComparison.CurrentCultureIgnoreCase))
                    {
                        _output[1] += _error[1];
                    }
                    if (output.Length > _maxOutputLength)
                    {
                        output = output.Substring(0, _maxOutputLength);
                        output += "... (Output truncated)";
                    }
                    _output[1] += output;
                    return _output;
                }
                else
                {
                    return _error;
                }
            }
            else
            {
                return _error;
            }
        }
    }

    public string GetTempFile()
    {
        return tempFile;
    }

    public void DeleteTempFile()
    {
        System.IO.File.Delete(tempFile);
    }

    public void AppendToTempFile(string code)
    {
        code = code.Remove(0, _languageLength);
        System.IO.File.AppendAllText(tempFile, code);
    }

    static bool IsPythonInstalled()
    {
        ProcessStartInfo pythonCheck = new ProcessStartInfo
        {
            FileName = "python.exe",
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using Process process = Process.Start(pythonCheck);
        string result = process.StandardOutput.ReadToEnd();
        return !string.IsNullOrWhiteSpace(result);
    }

    private async Task<string> ReadStreamAsync(StreamReader reader, StringBuilder builder)
    {
        string line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            builder.AppendLine(line);
        }
        return builder.ToString();
    }
}
