using System.CommandLine;
using System.Security;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using ShellCopilot.Kernel;
using Spectre.Console;

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
        Option<bool> use_alt_buffer = new("--use-alt-buffer", "Use the alternate screen buffer for an interactive session.");

        query.AddValidator(result =>
        {
            string value = result.GetValueForArgument(query);

            if (value is not null && value.StartsWith('-'))
            {
                result.ErrorMessage = $"Bad flag or option syntax: {value}";
            }
        });

        RootCommand rootCommand = new("AI for the command line.")
        {
            query, use_alt_buffer,
            GetRegisterCommand(),
            GetUnregisterCommand(),
            GetListCommand(),
            GetGetCommand(),
            GetSetCommand(),
            GetUseCommand(),
            GetExportCommand(),
            GetImportCommand(),
        };

        rootCommand.SetHandler(StartShellAsync, query, use_alt_buffer);
        return rootCommand.Invoke(args);
    }

    private async static Task StartShellAsync(string query, bool use_alt_buffer)
    {
        Shell shell;
        if (query is not null)
        {
            shell = new(interactive: false);

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
            using var _ = ConsoleRender.UseErrorConsole();

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(ConsoleRender.FormatError("Cannot run interactively when the stdin, stdout, or stderr is redirected."));
            AnsiConsole.MarkupLine(ConsoleRender.FormatError("To run non-interactively, specify the <query> argument and try again."));
            return;
        }

        shell = new(interactive: true, use_alt_buffer);
        await shell.RunREPLAsync();
    }

    private static string GetSystemPromptForChat(string prompt)
    {
        // Check if 'prompt' points to a file
        if (prompt.Length <= 256
            && prompt.IndexOfAny(Path.GetInvalidPathChars()) is -1
            && prompt.IndexOfAny(Path.GetInvalidFileNameChars()) is -1)
        {
            string prompt_path = Path.GetFullPath(prompt);
            if (File.Exists(prompt_path))
            {
                prompt = File.ReadAllText(prompt_path);
            }
        }

        return prompt;
    }

    private async static Task<string> PromptForModelName(List<AiModel> models, CancellationToken cancellationToken)
    {
        return await new SelectionPrompt<string>()
            .Title("Select the model to be acted on:")
            .MoreChoicesText("[grey](Move up and down to reveal more model names)[/]")
            .AddChoices(models.Select(m => m.Name))
            .ShowAsync(AnsiConsole.Console, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Command GetRegisterCommand()
    {
        // Mandatory options
        Option<string> name = new("--name", "Name of the custom AI model.") { IsRequired = true };
        Option<string> endpoint = new("--endpoint", "The HTTPS endpoint of the model.") { IsRequired = true };
        Option<string> deployment = new("--deployment", "The deployment id of the Azure OpenAI service.") { IsRequired = true };
        Option<string> open_ai_model = new("--openai-model", "Name of the OpenAI model used by the deployment and its version, e.g. gpt-4-0613") { IsRequired = true };
        Option<string> prompt = new("--system-prompt", "The system prompt used for this custom AI model.") { IsRequired = true };

        // Optional options
        Option<string> description = new("--description", "Description of this custom AI model.");
        Option<string> key = new("--key", "The key used to authenticate with the endpoint.");
        Option<TrustLevel> trust_level = new("--trust-level", getDefaultValue: () => TrustLevel.Public, "The trust level of the custom AI model.");

        Command registerCmd = new("register", "Register a custom AI model.")
        {
            name,
            endpoint,
            deployment,
            open_ai_model,
            prompt,
            description,
            key,
            trust_level
        };

        registerCmd.SetHandler(
            Handler,
            name,
            endpoint,
            deployment,
            open_ai_model,
            prompt,
            description,
            key,
            trust_level);

        return registerCmd;

        static void Handler(
            string name,
            string endpoint,
            string deployment,
            string open_ai_model,
            string system_prompt,
            string description,
            string key,
            TrustLevel trust_level)
        {
            SecureString secure_key = Utils.ConvertDataToSecureString(key);
            system_prompt = GetSystemPromptForChat(system_prompt);

            Configuration
                .ReadFromConfigFile()
                .AddModels(new AiModel(
                    name,
                    description,
                    system_prompt,
                    endpoint,
                    deployment,
                    open_ai_model,
                    secure_key,
                    trust_level));
        }
    }

    private static Command GetUnregisterCommand()
    {
        Argument<string> name = new("name", getDefaultValue: () => null, "Name of the custom AI model.");
        Command unregisterCmd = new("unregister", "Unregister a custom AI model.") { name };

        unregisterCmd.SetHandler(async (context) =>
        {
            string nameValue = context.ParseResult.GetValueForArgument(name);
            await Handler(nameValue, context.GetCancellationToken()).ConfigureAwait(false);
        });

        return unregisterCmd;

        static async Task Handler(string name, CancellationToken cancellationToken)
        {
            var config = Configuration.ReadFromConfigFile();
            name ??= await PromptForModelName(config.Models, cancellationToken).ConfigureAwait(false);
            config.RemoveModel(name);
        }
    }

    private static Command GetListCommand()
    {
        Command listCmd = new("list", "List all available custom AI models.");
        listCmd.SetHandler(Handler);
        return listCmd;

        static void Handler()
        {
            Configuration config = Configuration.ReadFromConfigFile();
            config.ListAllModels();
        }
    }

    private static Command GetGetCommand()
    {
        Argument<string> name = new("name", getDefaultValue: () => null, "Name of the custom AI model.");
        Command getCmd = new("get", "Show the details of one custom AI model.") { name };

        getCmd.SetHandler(async (context) =>
        {
            string nameValue = context.ParseResult.GetValueForArgument(name);
            await Handler(nameValue, context.GetCancellationToken()).ConfigureAwait(false);
        });

        return getCmd;

        static async Task Handler(string name, CancellationToken cancellationToken)
        {
            var config = Configuration.ReadFromConfigFile();
            name ??= await PromptForModelName(config.Models, cancellationToken).ConfigureAwait(false);
            config.ShowOneModel(name);
        }
    }

    private static Command GetSetCommand()
    {
        // Mandatory options
        Option<string> name = new("--name", "Name of the custom AI model.") { IsRequired = true };

        // Optional options
        Option<string> description = new("--description", "Description of this custom AI model.");
        Option<string> endpoint = new("--endpoint", "The HTTPS endpoint of the model.");
        Option<string> deployment = new("--deployment", "The deployment id of the Azure OpenAI service.");
        Option<string> open_ai_model = new("--openai-model", "Name of the OpenAI model used by the deployment and its version, e.g. gpt-4-0613");
        Option<string> prompt = new("--system-prompt", "The system prompt used for this custom AI model.");
        Option<string> key = new("--key", "The key used to authenticate with the endpoint.");
        Option<TrustLevel?> trust_level = new("--trust-level", "The trust level of the custom AI model.");

        Command setCmd = new("set", "Change the information of an existing custom AI model.")
        {
            name,
            description,
            endpoint,
            deployment,
            open_ai_model,
            prompt,
            key,
            trust_level
        };

        setCmd.SetHandler(
            Handler,
            name,
            description,
            endpoint,
            deployment,
            open_ai_model,
            prompt,
            key,
            trust_level);

        return setCmd;

        static void Handler(
            string name,
            string description,
            string endpoint,
            string deployment,
            string open_ai_model,
            string system_prompt,
            string key,
            TrustLevel? trust_level)
        {
            Configuration config = Configuration.ReadFromConfigFile();
            AiModel model = config.GetModelByName(name)
                ?? throw new ArgumentException($"A model with the name <{name}> cannot be found.", nameof(name));

            bool updated = false;

            if (description is not null)
            {
                updated = true;
                model.Description = description;
            }
            if (endpoint is not null)
            {
                updated = true;
                model.Endpoint = endpoint;
            }
            if (deployment is not null)
            {
                updated = true;
                model.Deployment = deployment;
            }
            if (open_ai_model is not null)
            {
                updated = true;
                model.OpenAIModel = open_ai_model;
            }
            if (system_prompt is not null)
            {
                updated = true;
                model.SystemPrompt = GetSystemPromptForChat(system_prompt);
            }
            if (key is not null)
            {
                updated = true;
                model.Key = Utils.ConvertDataToSecureString(key);
            }
            if (trust_level is not null)
            {
                updated = true;
                model.TrustLevel = trust_level.Value;
            }

            if (updated)
            {
                Configuration.WriteToConfigFile(config);
                AnsiConsole.MarkupLineInterpolated($"[bold green]Model <{name}> was updated.[/]");
            }
            else
            {
                AnsiConsole.WriteLine("[bold yellow]Model <{name}> was not changed.[/]");
            }
        }
    }

    private static Command GetExportCommand()
    {
        Option<string> name = new("--name", "Name of the custom AI model.");
        Option<FileInfo> file = new("--file", "The file to export the model information to.");
        Option<bool> include_key = new("--include-key", "Include the key in the export.");

        Command exportCmd = new("export", "Export the model registration information.") { name, file, include_key };
        exportCmd.SetHandler(Handler, name, file, include_key);
        return exportCmd;

        static void Handler(string name, FileInfo file, bool include_key)
        {
            bool ignoreKey = !include_key;
            Configuration config = Configuration.ReadFromConfigFile();
            string result = config.ExportModel(name, file, ignoreKey);

            if (result is not null)
            {
                Console.WriteLine();
                Console.WriteLine(result);
            }
        }
    }

    private static Command GetImportCommand()
    {
        Argument<FileInfo> file = new("file", "The exported JSON file for model registration information.");
        Command importCmd = new("import", "Import model registration information from a file.") { file };

        importCmd.SetHandler(Handler, file);
        return importCmd;

        static void Handler(FileInfo file)
        {
            using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var models = JsonSerializer.Deserialize<AiModel[]>(stream, options);
            Configuration config = Configuration.ReadFromConfigFile();
            config.AddModels(models);
        }
    }

    private static Command GetUseCommand()
    {
        // Mandatory options
        Argument<string> name = new("name", getDefaultValue: () => null, "Name of the custom AI model.");

        Command useCmd = new("use", "Choose the custom AI model that will be used for operations.") { name };
        useCmd.SetHandler(async (context) =>
        {
            string nameValue = context.ParseResult.GetValueForArgument(name);
            await Handler(nameValue, context.GetCancellationToken()).ConfigureAwait(false);
        });

        return useCmd;

        static async Task Handler(string name, CancellationToken cancellationToken)
        {
            var config = Configuration.ReadFromConfigFile();
            name ??= await PromptForModelName(config.Models, cancellationToken).ConfigureAwait(false);
            config.UseModel(name, keyRequired: false, cancellationToken);
        }
    }
}
