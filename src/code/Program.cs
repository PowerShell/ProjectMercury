using System.CommandLine;
using Spectre.Console;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using System.Reflection;
using System.Management.Automation;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace Microsoft.PowerShell.Copilot
{
    internal class History
    {
        public required List<string>? history {get; set;}
        
    }
    
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand("ai tool allowing use for model and endpoints");
            rootCommand.AddAlias("ai");
            addModelCommand(args, rootCommand); 
            Option restore = new Option<bool>("--restore", "Restore History");
            rootCommand.Add(restore);

            Argument query = new Argument<string>("query", "question to ask AI");
            rootCommand.AddArgument(query);
            query.SetDefaultValue("");

            var parsedArgs = rootCommand.Parse(args);
            
            bool restoreValue = (bool)(parsedArgs.GetValueForOption(restore) ?? false);

            if(ModelFunctions.getCurrentModel() == null)
            {
                addDefaultModel();
            }

            Action action = delegate() 
            { 
                try
                {
                    if(args.Count() == 0 || restoreValue)
                    {
                        Initialize(restoreValue);
                    }
                    else
                    {
                        var inputData = string.Empty;
                        if (!Console.IsInputRedirected)
                        {
                            string firstArgument = args[0];
                            inputData += firstArgument;
                        }
                        else
                        {
                            inputData += Console.In.ReadToEnd();
                        }

                        OpenAI nonInteractive = new OpenAI();
                        AnsiConsole.Status().Spinner(Spinner.Known.Star).Start("Thinking...", ctx => {
                        AnsiConsole.MarkupInterpolated($"{PSStyle.Instance.Foreground.BrightYellow}{nonInteractive.GetCompletion(inputData, false, new System.Threading.CancellationToken())}\n");
                        });
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }

            };
            rootCommand.SetHandler(action);
            await rootCommand.InvokeAsync(args);
        }

        private static ParseResult parse(string[] args, Command command)
        {
            var parser = new Parser(command);
            var parsedArgs = parser.Parse(args);
            parsedArgs.Invoke();
            return parsedArgs;
        }

        private static void addModelCommand(string[] args, Command mainCommand)
        {
            Command newModel = new Command("register", "Create a new model for use") { };
            Command unregisterModel = new Command("unregister", "Unregister the specified model");
            Command useModel = new Command("use", "Use the specified model");
            Command getModel = new Command("get", "Get the registration information of a model");
            Command setModel = new Command("set", "Set the registration information of a model");
            Command listModels = new Command("list", "List the registration information of all registered models");
            Command exportModel = new Command("export", "Export the registration information of a model");
            Command importModel = new Command("import", "Import the registration information of a model");

            //Creating options for new
            addOptionAndAlias(newModel, "--name", " Name of the model", "-n");
            addOptionAndAlias(newModel, "--description", "The description of the model", "-d");
            addOptionAndAlias(newModel, "--endpoint", "The HTTPS endpoint of the model", "-e");
            addOptionAndAlias(newModel, "--key", "The API key for the model", "-k");
            addOptionAndAlias(newModel, "--deployment", "The name of the deployment", "-m");
            addOptionAndAlias(newModel, "--openai-model", "The name of the OpenAI model used by the deployment", "-o");
            addOptionAndAlias(newModel, "--system-prompt", "The system prompt for the model", "-p");
            addOptionAndAlias(newModel, "--trust", "The trust level of the model: public or private. Default is public.");

            var parsedArgs = newModel.Parse(args);
            string? name = parsedArgs.GetValueForOption(newModel.Options[0])?.ToString() ?? null;
            string? description = parsedArgs.GetValueForOption(newModel.Options[1])?.ToString() ?? null;
            string? endpointUrl = parsedArgs.GetValueForOption(newModel.Options[2])?.ToString() ?? null;
            string? key = parsedArgs.GetValueForOption(newModel.Options[3])?.ToString() ?? null;
            string? deployment = parsedArgs.GetValueForOption(newModel.Options[4])?.ToString() ?? null;
            string? openaiModel = parsedArgs.GetValueForOption(newModel.Options[5])?.ToString() ?? null;
            string? systemPrompt = parsedArgs.GetValueForOption(newModel.Options[6])?.ToString() ?? null;
            string? trust = parsedArgs.GetValueForOption(newModel.Options[7])?.ToString() ?? null;

            Action action = delegate() { ModelFunctions.addModel(name, description, endpointUrl, key, deployment, openaiModel, systemPrompt, trust);} ;
            newModel.SetHandler(action);

            //set command
            addOptionAndAlias(setModel, "--name", " Name of the model", "-n");
            addOptionAndAlias(setModel, "--description", "Description of the model", "-d");
            addOptionAndAlias(setModel, "--endpoint", "Endpoint URL to use for this model", "-e");
            addOptionAndAlias(setModel, "--key", "The API key for the model", "-k");
            addOptionAndAlias(setModel, "--deployment", "The deployment id", "-m");
            addOptionAndAlias(setModel, "--openai-model", "The name of the OpenAI model used by the deployment", "-o");
            addOptionAndAlias(setModel, "--system-prompt", "The system prompt for the model", "-p");
            addOptionAndAlias(setModel, "--trust", "The trust level of the model");

            parsedArgs = setModel.Parse(args);
            name = parsedArgs.GetValueForOption(setModel.Options[0])?.ToString() ?? null;
            description = parsedArgs.GetValueForOption(setModel.Options[1])?.ToString() ?? null;
            endpointUrl = parsedArgs.GetValueForOption(setModel.Options[2])?.ToString() ?? null;
            key = parsedArgs.GetValueForOption(setModel.Options[3])?.ToString() ?? null;
            deployment = parsedArgs.GetValueForOption(setModel.Options[4])?.ToString() ?? null;
            openaiModel = parsedArgs.GetValueForOption(setModel.Options[5])?.ToString() ?? null;
            systemPrompt = parsedArgs.GetValueForOption(setModel.Options[6])?.ToString() ?? null;
            trust = parsedArgs.GetValueForOption(setModel.Options[7])?.ToString() ?? null;

            action = delegate() { ModelFunctions.setAModel(name, description, endpointUrl, key, deployment, openaiModel, systemPrompt, trust);} ;
            setModel.SetHandler(action);

            //list command
            action = delegate() { ModelFunctions.listAllModels();} ;
            listModels.SetHandler(action);

            //export command
            addOptionAndAlias(exportModel, "--name", " Name of the model", "-n");
            addOptionAndAlias(exportModel, "--file", "Name of the file to export model into", "-f");
            addOptionAndAlias(exportModel, "--all", "Export all registered models");
            addOptionAndAlias(exportModel, "--show-keys", "Export registered models with api key(s) included");

            parsedArgs = exportModel.Parse(args);
            name = parsedArgs.GetValueForOption(exportModel.Options[0])?.ToString() ?? null;
            string? file = parsedArgs.GetValueForOption(exportModel.Options[1])?.ToString() ?? null;
            bool all = (bool)(parsedArgs.GetValueForOption(exportModel.Options[2]) ?? false);
            bool showKeys = (bool)(parsedArgs.GetValueForOption(exportModel.Options[3]) ?? false);

            action = delegate() { ModelFunctions.exportAModel(file, name, all, showKeys);} ;
            exportModel.SetHandler(action);

            //import command
            addOptionAndAlias(importModel, "--file", "Name of the file to export model into", "-f");
            parsedArgs = importModel.Parse(args);
            file = parsedArgs.GetValueForOption(importModel.Options[0])?.ToString() ?? null;

            action = delegate() { ModelFunctions.importAModel(file);} ;
            importModel.SetHandler(action);



            //remove command
            addOptionAndAlias(unregisterModel, "--name", " Name of the model", "-n");
            addOptionAndAlias(unregisterModel, "--all", "Export all registered models");
            parsedArgs = unregisterModel.Parse(args);
            string? unregisterName = parsedArgs.GetValueForOption(unregisterModel.Options[0])?.ToString() ?? null;
            all = (bool)(parsedArgs.GetValueForOption(unregisterModel.Options[1]) ?? false);
            action = delegate() { ModelFunctions.unregisterAModel(unregisterName, all);} ;
            unregisterModel.SetHandler(action);

            //use command
            addOptionAndAlias(useModel, "--name", " Name of the model", "-n");
            parsedArgs = useModel.Parse(args);
            string? useName = parsedArgs.GetValueForOption(useModel.Options[0])?.ToString() ?? null;
            action = delegate() { ModelFunctions.setCurrentModel(useName);} ;
            useModel.SetHandler(action);

            //get command
            addOptionAndAlias(getModel, "--name", " Name of the model", "-n");
            parsedArgs = getModel.Parse(args);
            string? getName = parsedArgs.GetValueForOption(getModel.Options[0])?.ToString() ?? null;
            action = delegate() { Console.WriteLine(ModelFunctions.getSpecifiedModel(getName));} ;
            getModel.SetHandler(action);


            //adding new and remove to endpoint
            mainCommand.AddCommand(newModel);
            mainCommand.AddCommand(setModel);
            mainCommand.AddCommand(unregisterModel);
            mainCommand.AddCommand(useModel);
            mainCommand.AddCommand(getModel);
            mainCommand.AddCommand(listModels);
            mainCommand.AddCommand(exportModel);
            mainCommand.AddCommand(importModel);
        }


        private static Option addOptionAndAlias(Command command, string name, string description, string? alias = null)
        {
            if(name.Equals("--all") || name.Equals("--show-keys") || name.Equals("--restore"))
            {
                var newOption = new Option<bool>(name: name, description: description);
                newOption.Arity = ArgumentArity.Zero;
                command.AddOption(newOption);
                return newOption;
            }
            else
            {
                var newOption = new Option<string>(name: name, description: description);
                if(alias != null)
                {
                    newOption.AddAlias(alias);
                }
                command.AddOption(newOption);
                return newOption;
            }
        }



        internal static int GetParentProcessID()
        {
            var parent = ParentProcessUtilities.GetParentProcess();
            if(parent != null)
            {
                return parent.Id;
            }
            return -1;
        }

    

        internal static void addToHistory(string input)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string filepath = Path.Combine(currentDirectory, "history" + GetParentProcessID() + ".json");
            if(File.Exists(filepath))
            {
                string jsonString = File.ReadAllText(filepath);
                History history = JsonSerializer.Deserialize<History>(jsonString)!;
                history.history?.Add(input);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedHistory = JsonSerializer.Serialize(history, options);
                File.WriteAllText(filepath, updatedHistory);
            }
            else
            {
                List<string> newEntry = new List<string>();
                newEntry.Add(input);
                var history = new History {history = newEntry};
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(history, options);
                File.WriteAllText(filepath, jsonString);
            }
            
        }

        internal static void clearHistory()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string filepath = Path.Combine(currentDirectory, "history" + GetParentProcessID() + ".json");
            History updatedhistory = new History()
            {
                history = new List<string>()
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string updatedHistory = JsonSerializer.Serialize(updatedhistory, options);
            File.WriteAllText(filepath, updatedHistory);
        }

        internal static void printHistory(bool print)
        {
            if(print)
            {
                string currentDirectory = Directory.GetCurrentDirectory();
                string filepath = Path.Combine(currentDirectory, "history" + GetParentProcessID() + ".json");
                //string contents = File.ReadAllText(filepath);
                Screenbuffer.WriteConsole($"{Screenbuffer.RESET}");
                if(File.Exists(filepath))
                {
                    string jsonString = File.ReadAllText(filepath);
                    History history = JsonSerializer.Deserialize<History>(jsonString)!;
                    for(int i = 0; i < history.history?.Count; i++)
                    {
                        if(i % 2 == 0)
                        {
                            Screenbuffer.WriteConsole("\n" + Readline.PROMPT);
                            Screenbuffer.WriteLineConsole(history.history[i].ToString().TrimEnd());
                        }
                        else
                        {
                            var colorOutput = new StringBuilder();
                            colorOutput.AppendLine($"{PSStyle.Instance.Foreground.BrightYellow}{history.history[i]}");
                            Screenbuffer.WriteConsole($"{colorOutput.ToString()}{Screenbuffer.RESET}");
                        }
                    }
                }
            }
        }

        internal static void Initialize(bool restore)
        {
            var current = ModelFunctions.getCurrentModel();

            if(current != null && current?.Name.ToLower() == "default" && current.ApiKey == null)
            {
                Action action = delegate() { 
                    welcome();
                    var answer = AnsiConsole.Confirm("\nWould you like to begin using the Default Model (y/n)...");

                    if(answer)
                    {
                        var apiKey = AnsiConsole.Prompt(new TextPrompt<string>("Enter your API key: ").Secret());
                        current.ApiKey = apiKey;
                        ModelFunctions.setAModel("Default", null, null, apiKey, null, null, null, null);
                        ModelFunctions.setCurrentModel("Default");
                        new Microsoft.PowerShell.Copilot.EnterCopilot(restore);
                    }
                ;};
                AnsiConsole.AlternateScreen(action);
            }
            else if(current != null && current.ApiKey != null)
            {
                new Microsoft.PowerShell.Copilot.EnterCopilot(restore);
            }
            else if(current != null)
            {
                Console.WriteLine("Api key has not been set.");
            }
            else
            {
                Console.WriteLine("Active Model has not been set.");
            }
        }

        static internal void welcome()
        {
            AnsiConsole.Write(new FigletText("Shell Copilot").LeftJustified().Color(Color.DarkGoldenrod));
            var rule = new Rule();
            AnsiConsole.Write(rule);
            string intro = "\nThis module includes AI model management with the ability to enable an interactive chat mode as well as getting the last error and sending to GPT. \n";
            AnsiConsole.MarkupLineInterpolated($"{intro}");

            var grid = new Grid();

            Dictionary<string,string> commands = new Dictionary<string, string>{
                {"register","Create a new model for use"}, 
                {"set", "Set the registration information of a model"}, 
                {"unregister", "Unregister the specified model"},
                {"use", "Use the specified model"},
                {"get", "Get the registration information of a model"},
                {"list", "List the registration information of all registered models"},
                {"export", "Export the registration information of a model"},
                {"import", "Import the registration information of a model"}
            };

            grid.AddColumn();
            grid.AddColumn();
            
            grid.AddRow(new string[] {"Commands: ", ""});
            foreach(KeyValuePair<string, string> kvp in commands)
            {
                grid.AddRow(new string[]{turnToGolden(kvp.Key), kvp.Value});
            }
            grid.Width = 100;
            AnsiConsole.Write(grid);

            AnsiConsole.MarkupLine("\n[bold]Registering A New Model: [/]\n\nIf you would like to register a model into the system, please enter the" + turnToGolden("register") + "command with the following fields: \n");

            grid = new Grid();

            Dictionary<string,string> options = new Dictionary<string, string>{
                {"-n, --name","Name of the model"}, 
                {"-d, --description (optional)", "Description of the model"}, 
                {"-e, --endpoint", "Endpoint URL to use for this model"},
                {"-k, --key (optional)", "The API key for the model"},
                {"-m, --deployment", "The deployment id"},
                {"-o, --openai-model (optional)", "The name of the OpenAI model used by the deployment"},
                {"-p, --system-prompt", "The system prompt for the model"},
                {"--trust (optional)", "The trust level of the model"},
                {"-?, -h, --help", "Show help and usage information"}
            };

            grid.AddColumn();
            grid.AddColumn();
            
            grid.AddRow(new string[] {"Options: ", ""});
            foreach(KeyValuePair<string, string> kvp in options)
            {
                string key = turnToGolden(kvp.Key);
                key = key.Replace("(optional)", "[grey69](optional)[/]" );
                grid.AddRow(new string[]{key, kvp.Value});
            }
            grid.Width = 100;
            AnsiConsole.Write(grid);

            var answer = AnsiConsole.Confirm("\nWould you to continue to the Default Model Access Information?...");

            if(answer)
            {
                AnsiConsole.MarkupLine("\n[bold]Using the Default Microsoft Model: [/]\n");
                string message = "The default Microsoft Model has already been registered in the system.\n" + "If you would like to gain access to the model, follow the instructions below to gain access to an API Key:\n";
                AnsiConsole.MarkupLine(message);

                grid = new Grid();

                Dictionary<string,string> instructions = new Dictionary<string, string>{
                    {"1.", "Navigate to https://pscopilot.developer.azure-api.net"},
                    {"2.", "Click `Sign Up` located on the top right corner of the page."},
                    {"3.", "Sign up for a subscription by filling in the fields (email, password,first name, last name)."},
                    {"4.", "Verify the account (An email should have been sent from apimgmt-noreply@mail.windowsazure.com to your email)"},
                    {"5.", "Click `Sign In` located on the top right corner of the page."},
                    {"6.", "Enter the email and password used when signing up."},
                    {"7.", "Click `Products` located on the top right corner of the page"},
                    {"8.", "In the field stating `Your new product subscription name`, Enter `Azure OpenAI Service API`."},
                    {"9.", "Click `Subscribe` to be subscribed to the product.\n"},
                    {"", "In order to view your subscription/API key,"},
                    {"1. ", "Click `Profile` located on the top right corner of the page."},
                    {"2. ", "Your Key should be located under the `Subscriptions` section."},
                };

                grid.AddColumn();
                grid.AddColumn();
                foreach(KeyValuePair<string, string> kvp in instructions)
                {
                    if(kvp.Value.Contains("`"))
                    {
                        string value = kvp.Value;
                        char delimiter = '`';
                        string[] words = value.Split(delimiter);
                        words[1] = turnToGolden(words[1]);
                        words[1] = words[1].Replace("] ", "]");
                        words[1] = words[1].Replace(" [", "[");
                        grid.AddRow(new string[]{kvp.Key, words[0] + words[1] + words[2]});
                    }
                    else
                    {
                        grid.AddRow(new string[]{kvp.Key, kvp.Value});
                    }
                }
                grid.Width = 70;
                AnsiConsole.Write(grid);
                AnsiConsole.MarkupLine("Click on" + turnToGolden("Show") + "to view the primary or secondary key.\n");
            }
        }

        static private string turnToGolden (string words)
        {
            return $"[darkgoldenrod] {words} [/]";
        }

        static private void welcomeBreak()
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void addDefaultModel()
        {
            ModelFunctions.addModel("Default", "The default model", "https://pscopilot.azure-api.net", null, "gpt4", "gpt4", $"You are an AI assistant with experise in PowerShell, Azure, and the command line.  Assume user is using {OpenAI.GetOS()} operating system unless specified. You are helpful, creative, clever, and very friendly. Responses including PowerShell code are enclosed in ```powershell blocks." ,"public");
        }

    }

}

