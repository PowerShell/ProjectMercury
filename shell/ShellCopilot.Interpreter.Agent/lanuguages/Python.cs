using System;
using System.Linq;
using System.Diagnostics;
using ShellCopilot.Abstraction;
using System.Security.Claims;

public class Python
{
    private string _code { get; set; }
    private string[] _error { get; set; }
    private string[] _output { get; set; }
    private int _languageLength = "python\n".Length;
    private string tempFile;

    public Python(string code)
    {
        _code = code.Remove(0, _languageLength);
        tempFile = System.IO.Path.GetTempFileName();
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
            // write the code to a file
            tempFile = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllText(tempFile, _code);

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
            await Task.Run(() => process.WaitForExit());
            _error = new string[2];
            _error[0] = "error";
            _error[1] = process.StandardError.ReadToEnd();

            //delete the file
            System.IO.File.Delete(tempFile);
            if (string.IsNullOrWhiteSpace(_error[1]))
            {
                _output = new string[2];
                _output[0] = "output";
                _output[1] = process.StandardOutput.ReadToEnd();
                return _output;
            }
            else
            {
                return _error;
            }
        }
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
}
