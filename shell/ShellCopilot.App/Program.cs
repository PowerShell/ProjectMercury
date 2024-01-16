using System.CommandLine;
using System.Text;
using System.Text.Json;
using ShellCopilot.Abstraction;
using ShellCopilot.Kernel;

namespace ShellCopilot.App;

internal class Program
{
    static int Main(string[] args)
    {
        // TODO-1: Currently the syntax is `aish [<query>] [command] [options]`, namely,
        // one can run `aish "hello" list`, and it will execute the list command.
        // Ideally, we want it to be like dotnet, where `dotnet applicat-path` is clearly
        // separated from the `dotnet sdk` commands such as `dotnet run`.
        //
        // TODO-2: Add exception handling. The default exception handling is just to write
        // out the stack trace. We need to have our own exception handling to make it less
        // scary and more useful.
        //
        // TODO-3: System.CommandLine is undergoing lots of design changes, with breaking
        // changes to the existing public APIs. We will need to evaluate whether we want to
        // keep depending on it when this project moves beyond a prototype.

        Console.OutputEncoding = Encoding.Default;
        Argument<string> query = new("query", getDefaultValue: () => null, "The query term used to get response from AI.");
        Option<FileInfo> shellWrapper = new("--shell-wrapper", "Path to the configuration file to wrap Shell Copilot as a different application.");

        query.AddValidator(result =>
        {
            string value = result.GetValueForArgument(query);

            if (value is not null && value.StartsWith('-'))
            {
                result.ErrorMessage = $"Bad flag or option syntax: {value}";
            }
        });

        RootCommand rootCommand = new("AI for the command line.") { query, shellWrapper };
        rootCommand.SetHandler(StartShellAsync, query, shellWrapper);
        return rootCommand.Invoke(args);
    }

    private async static Task StartShellAsync(string query, FileInfo shellWrapperConfigFile)
    {
        if (!ReadShellWrapperConfig(shellWrapperConfigFile, out ShellWrapper shellWrapper))
        {
            return;
        }

        Utils.Setup(shellWrapper?.Name);

        Shell shell;
        if (query is not null)
        {
            shell = new(interactive: false, shellWrapper);

            if (Console.IsInputRedirected)
            {
                string context = Console.In.ReadToEnd();
                if (context is not null && context.Length > 0)
                {
                    query = string.Concat(query, "\n\n", context);
                }
            }

            await shell.RunOnceAsync(query);
            return;
        }

        if (Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Cannot run interactively when the stdin, stdout, or stderr is redirected.");
            Console.Error.WriteLine("To run non-interactively, specify the <query> argument and try again.");
            return;
        }

        shell = new(interactive: true, shellWrapper);
        await shell.RunREPLAsync();
    }

    private static bool ReadShellWrapperConfig(FileInfo file, out ShellWrapper shellWrapper)
    {
        shellWrapper = null;

        if (file is null)
        {
            return true;
        }

        if (!file.Exists)
        {
            Console.Error.WriteLine($"The specified config file '{file.FullName}' doesn't exist.");
            return false;
        }

        try
        {
            using var stream = file.OpenRead();
            var options = Utils.GetJsonSerializerOptions();

            shellWrapper = JsonSerializer.Deserialize<ShellWrapper>(stream, options);
            if (string.IsNullOrEmpty(shellWrapper.Name) || string.IsNullOrEmpty(shellWrapper.Banner) ||
                string.IsNullOrEmpty(shellWrapper.Version) || string.IsNullOrEmpty(shellWrapper.Prompt) ||
                string.IsNullOrEmpty(shellWrapper.Agent))
            {
                Console.Error.WriteLine("Invalid shell wrapper configuration. Make sure the following required keys are properly set:");
                Console.Error.WriteLine($"{nameof(ShellWrapper.Name)}, {nameof(ShellWrapper.Banner)}, {nameof(ShellWrapper.Version)}, {nameof(ShellWrapper.Prompt)}, {nameof(ShellWrapper.Agent)}");

                shellWrapper = null;
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Loading JSON configuration from '{file.FullName}' failed: {ex.Message}");
            return false;
        }

        return true;
    }
}
