using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using System.Linq;
using Spectre.Console;

namespace Microsoft.PowerShell.Copilot
{
    [Serializable]
    public class Configuration
    {
        public List<Model> models {get; set;}
        public Model? activeModel {get; set;}

        public Configuration()
        {
            models = new List<Model>();
            activeModel = null;
        }
        
    }

    

    [Serializable]
    public class Model
    {
        public required string Name {get; set;}
        public string? Description {get; set;}
        public required string Endpoint {get; set;}
        [YamlIgnore]
        public string? ApiKey {get; set;}
        public required string Deployment {get; set;}
        public string? OpenAI_Model {get; set;}
        public string? TrustLevel {get; set;}
        public required string Prompt {get; set;}
    }


    public class ModelFunctions
    {
        public static void logModel(Configuration modelList, string storageFile)
        { 
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(modelList, options);
            File.WriteAllText(storageFile, jsonString);
        }

        public static void logModel(Model model, string storageFile)
        { 
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(model, options);
            File.WriteAllText(storageFile, jsonString);
        }

        public static Configuration? readModels(string storageFile)
        {
            string jsonString = File.ReadAllText(storageFile);
            Configuration Configuration = JsonSerializer.Deserialize<Configuration>(jsonString)!;
            return Configuration;
        }

        public static Model? readModel(string storageFile)
        {
            string jsonString = File.ReadAllText(storageFile);
            Model model = JsonSerializer.Deserialize<Model>(jsonString)!;
            return model;
        }

        public static void exportModel(Model model, string? storageFile = null, bool? showKey = false)
        { 
            var options = new JsonSerializerOptions { WriteIndented = true };
            var modelProperties = new Object();
            if(showKey != null && showKey == false)
            {
                modelProperties = new
                {
                    Name = model.Name,
                    Description = model.Description,
                    Endpoint = model.Endpoint,
                    Deployment = model.Deployment,
                    OpenAI_Model = model.OpenAI_Model,
                    TrustLevel = model.TrustLevel,
                    Prompt = model.Prompt,
                };
            }
            else if(showKey == true)
            {
                modelProperties = model;
            }

            if(storageFile != null)
            {
                string jsonString = JsonSerializer.Serialize(modelProperties, options);
                if (Path.IsPathRooted(storageFile))
                {
                    File.WriteAllText(storageFile, jsonString);
                }
                else
                {
                    string currentDirectory = Directory.GetCurrentDirectory();
                    string filepath = Path.Combine(currentDirectory, storageFile);
                    File.WriteAllText(filepath, jsonString);
                }
            }
            else
            {
                string jsonString = JsonSerializer.Serialize(modelProperties, options);
                Console.WriteLine(jsonString); 
            }
        }

        public static void exportModel(Configuration modelList, string? storageFile = null, bool? showKey = false)
        { 
            var options = new JsonSerializerOptions { WriteIndented = true };
            Configuration allModels = new Configuration();
            var modelProperties = new Object();
            foreach(Model model in modelList.models)
            {
                if(showKey != null && showKey == false)
                {
                    modelProperties = new
                    {
                        Name = model.Name,
                        Description = model.Description,
                        Endpoint = model.Endpoint,
                        Deployment = model.Deployment,
                        OpenAI_Model = model.OpenAI_Model,
                        TrustLevel = model.TrustLevel,
                        Prompt = model.Prompt,
                    };
                }
                else if(showKey == true)
                {
                    modelProperties = model;
                }

                allModels.models.Add((Model) modelProperties);
            }

            if(storageFile != null)
            {

                string jsonString = JsonSerializer.Serialize(allModels, options);
                if (Path.IsPathRooted(storageFile))
                {
                    File.WriteAllText(storageFile, jsonString);
                }
                else
                {
                    string currentDirectory = Directory.GetCurrentDirectory();
                    string filepath = Path.Combine(currentDirectory, storageFile);
                    File.WriteAllText(storageFile, jsonString);
                }
            }
            else
            {
                string jsonString = JsonSerializer.Serialize(allModels, options);
                Console.WriteLine(jsonString); 
            }
        }

        internal static void addModel(string? name, string? description, string? endpoint, string? apiKey, string? deployment, string? openaiModel, string? prompt, string? trust)
        {
            var allModels = getAllModels();
            var trustLevel = "public";

            if(allModels != null)
            {
                if(!allModels.models.Any(model => model.Name.ToLower() == name?.ToLower()))
                {
                    //var secretApiKey = apiKey?.ToString() ?? AnsiConsole.Prompt(new TextPrompt<string>("Enter the API key: ").Secret());
                    if(File.Exists(prompt))
                    {
                        prompt = File.ReadAllText(prompt);
                    }

                    if(trust != null && trust.ToLower().Equals("private"))
                    {
                        trustLevel = "private";
                    }
                    endpoint = endpoint?.TrimEnd('/').ToLower();
                    if(endpoint != null && !(endpoint.EndsWith(".azure-api.net", StringComparison.Ordinal) || endpoint.EndsWith(".openai.azure.com", StringComparison.Ordinal)))
                    {
                        throw new Exception($"The specified endpoint '{endpoint}' is not a valid Azure OpenAI service endpoint.");
                    }

                    if(openaiModel == null)
                    {
                        openaiModel = deployment;
                    }
                    
                    Model newModel = new Model()
                    {
                        Name = name ?? throw new Exception("Name needed"),
                        Description = description,
                        Endpoint = endpoint ?? throw new Exception("Endpoint Url needed"),
                        ApiKey = apiKey,
                        Deployment = deployment ?? throw new Exception("Deployment needed"),
                        OpenAI_Model = openaiModel,
                        TrustLevel = trustLevel,
                        Prompt = prompt ?? throw new Exception("Prompt needed")
                    };

                    allModels.models.Add(newModel);

                    ModelFunctions.logModel(allModels, getStorageFile());

                    if(allModels.models.Count == 1)
                    {
                        setCurrentModel(newModel.Name);
                    }
                }
                else
                {
                    Console.WriteLine($"{name} has already been registered");
                }
            }
        }

        internal static void setCurrentModel(string? modelName)
        {
            var allModels = getAllModels();


            if(allModels != null)
            {
                List<string> modelList = allModels.models.Select(model => model.Name).ToList();

                if(modelName != null)
                {
                    var modelUse = modelName?.ToString() ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose the model: ").AddChoices(modelList.ToArray<string>()));
                    Model? foundModel = allModels.models.Find(Model => Model.Name.ToLower() == modelUse?.ToLower());

                    if(foundModel != null && foundModel.ApiKey == null)
                    {
                        if(foundModel.Name.ToLower() != "default")
                        {
                            Console.WriteLine($"Please enter the API Key to use {foundModel.Name}.");
                            foundModel.ApiKey = AnsiConsole.Prompt(new TextPrompt<string>("Enter the API key: ").Secret());
                        }
                        allModels.activeModel = foundModel;
                        ModelFunctions.logModel(allModels, getStorageFile());
                    }
                    else if(foundModel != null)
                    {
                        allModels.activeModel = foundModel;
                        ModelFunctions.logModel(allModels, getStorageFile());
                    }
                    else
                    {
                        throw new Exception("Model not found");
                    }
                }
                else
                {
                    Model? foundModel = allModels.models.Find(Model => Model.Name.ToLower() == "default") ?? allModels.models[0];
                    if(foundModel != null)
                    {
                        allModels.activeModel = foundModel;
                        ModelFunctions.logModel(allModels, getStorageFile());
                    }
                }
            }
        }

        internal static Model? getCurrentModel()
        {
            var allModels = getAllModels();
            var current = allModels?.activeModel;
            return current;
        }

        internal static void exportAModel(string? file, string? modelName = null, bool? all = false, bool? showKey = false)
        {
            var allModels = getAllModels();

            if(allModels != null)
            {
                if(all != false)
                {
                    ModelFunctions.exportModel(allModels, file, showKey);
                }
                else
                {
                    List<string> modelList = allModels.models.Select(model => model.Name).ToList();
                    var modelExport = modelName?.ToString() ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose the model: ").AddChoices(modelList.ToArray<string>()));
                    Model? foundModel = allModels.models.Find(Model => Model.Name.ToLower() == modelExport?.ToLower());

                    if(foundModel != null)
                    {
                        ModelFunctions.exportModel(foundModel, file, showKey);
                    }
                    else
                    {
                        Console.WriteLine("Model not Found. ");
                    }
                }
            }
            else
            {
                Console.WriteLine("No models are registered. ");
            }
        }

        internal static void importAModel(string? file)
        {
            if(File.Exists(file))
            {
                try
                {
                    var imported = ModelFunctions.readModel(file);
                    if(imported != null)
                    {
                        addModel(imported.Name, imported.Description, imported.Endpoint, imported.ApiKey, imported.Deployment, imported.OpenAI_Model, imported.Prompt, imported.TrustLevel);
                    }
                }
                catch(Exception)
                {
                    var imported = ModelFunctions.readModels(file);
                    if(imported != null)
                    {
                        foreach(Model model in imported.models)
                        {
                            addModel(model.Name, model.Description, model.Endpoint, model.ApiKey, model.Deployment, model.OpenAI_Model, model.Prompt, model.TrustLevel);
                        }
                    }
                }
            }
            else if(file != null)
            {

                string currentDirectory = Directory.GetCurrentDirectory();
                file = Path.Combine(currentDirectory, file);
                if(File.Exists(file))
                {
                    try
                    {
                        var imported = ModelFunctions.readModel(file);
                        if(imported != null)
                        {
                            addModel(imported.Name, imported.Description, imported.Endpoint, imported.ApiKey, imported.Deployment, imported.OpenAI_Model, imported.Prompt, imported.TrustLevel);
                        }
                    }
                    catch(Exception)
                    {
                        var imported = ModelFunctions.readModels(file);
                        if(imported != null)
                        {
                            foreach(Model model in imported.models)
                            {
                                addModel(model.Name, model.Description, model.Endpoint, model.ApiKey, model.Deployment, model.OpenAI_Model, model.Prompt, model.TrustLevel);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("File is not found");
                }
                
            }
            else
            {
                Console.Write("File is needed");
            }
        }

        internal static void unregisterAModel(string? modelName = null, bool? all = false)
        {
            var allModels = getAllModels();
            if(allModels != null)
            {
                if(all != null && all == true)
                {
                    allModels.models.Clear();
                    allModels.activeModel = null;
                    ModelFunctions.logModel(allModels, getStorageFile());
                }
                else
                {
                    List<string> modelList = allModels.models.Select(model => model.Name).ToList();
                    var modelDelete = modelName?.ToString() ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose the model: ").AddChoices(modelList.ToArray<string>()));
                    Model? foundModel = allModels.models.Find(Model => Model.Name.ToLower() == modelDelete?.ToLower());

                    if(foundModel != null)
                    {
                        if(getCurrentModel() != null && foundModel.Name.Equals(getCurrentModel()?.Name))
                        {
                            setCurrentModel(null);
                        }
                        allModels.models.RemoveAll(model => model.Name.ToLower() == foundModel.Name.ToLower());
                        Console.WriteLine($"Model '{foundModel.Name}' uregistered");
                    }
                    else
                    {
                        Console.WriteLine("Model not Found. ");
                    }
                    ModelFunctions.logModel(allModels, getStorageFile());
                }
            }
            else
            {
                Console.WriteLine("No models are registered. ");
            }
        }

        internal static void listAllModels()
        {
            var allModels = getAllModels();

            if(allModels != null)
            {
                List<string> modelNameList = allModels.models.Select(model => model.Name).ToList();
                List<string?> modelTrustList = allModels.models.Select(model => model.TrustLevel).ToList();
                List<string?> modelDescriptionList = allModels.models.Select(model => model.Description).ToList();

                var table = new Table();

                table.AddColumn("name");
                table.AddColumn("active");
                table.AddColumn("trust");
                table.AddColumn("description");

                foreach(Model model in allModels.models)
                {
                    var modelAttributes = new List<string>{model.Name, "", model.TrustLevel ?? "", model.Description ?? ""}.ToArray();
                    if(getCurrentModel() != null && getCurrentModel()?.Name.ToLower() == model.Name.ToLower())
                    {
                        modelAttributes[1] = "yes";
                    }
                    table.AddRow(modelAttributes);
                }
                AnsiConsole.Write(table);
                
            }
            else
            {
                Console.WriteLine("No models are registered. ");
            }
        }

        internal static void setAModel(string? modelName, string? modelDescription, string? modelEndpointUrl, string? modelApiKey, string? modelDeployment, string? modelOpenAI, string? modelPrompt, string? modelTrustLevel)
        {
            var allModels = getAllModels();

            if(allModels != null)
            {
                List<string> modelList = allModels.models.Select(model => model.Name).ToList();
                var modelSet = modelName?.ToString() ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose the model: ").AddChoices(modelList.ToArray<string>()));
                Model? foundModel = allModels.models.Find(model => model.Name.ToLower() == modelSet?.ToLower());

                if(foundModel != null)
                {
                    foundModel.Description = modelDescription?.ToString() ?? foundModel.Description;
                    foundModel.Endpoint = modelEndpointUrl?.ToString() ?? foundModel.Endpoint;
                    foundModel.ApiKey = modelApiKey?.ToString() ?? foundModel.ApiKey;
                    foundModel.TrustLevel = modelTrustLevel?.ToString().ToLower() ?? foundModel.TrustLevel;
                    foundModel.Deployment = modelDeployment?.ToString() ?? foundModel.Deployment;
                    foundModel.OpenAI_Model = modelOpenAI?.ToString() ?? foundModel.OpenAI_Model;

                    var prompt = modelPrompt?.ToString();
                    if(File.Exists(prompt))
                    {
                        prompt = File.ReadAllText(prompt);
                    }
                    foundModel.Prompt = prompt ?? foundModel.Prompt;

                    if(foundModel.Name.ToLower().Equals(getCurrentModel()?.Name.ToLower()))
                    {
                        allModels.activeModel = foundModel;
                    }
                    ModelFunctions.logModel(allModels, getStorageFile());
                }
                else
                {
                    Console.WriteLine("Model not Found. ");
                }
            }
        }
        
        internal static string getSpecifiedModel(string? modelName)
        {
            var allModels = getAllModels();

            if(allModels != null)
            {
                List<string> modelList = allModels.models.Select(model => model.Name).ToList();
                var modelGet = modelName?.ToString() ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Choose the model: ").AddChoices(modelList.ToArray<string>()));
                Model? foundModel = allModels.models.Find(Model => Model.Name.ToLower() == modelGet?.ToLower());

                if(foundModel != null)
                {
                    var serializer = new SerializerBuilder().Build();
                    var yaml = serializer.Serialize(foundModel);
                    return yaml;
                }
                else
                {
                    return "Model not Found. ";
                }
            }
            else
            {
                return "No models are registered. ";
            }
        }

        internal static Configuration? getAllModels()
        {
            string storageFile = getStorageFile();
            var allModels = new Configuration();

            if(File.Exists(storageFile))
            {
                allModels = ModelFunctions.readModels(storageFile);
            }

            return allModels;
        }

        internal static string getStorageFile(bool? alternateFile = false)
        {
            if(alternateFile != null && alternateFile == true)
            {
                string storage = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string storageFolder = Path.Combine(storage, "ai");
                string storageFile = Path.Combine(storageFolder, "modelsTest.json");
                return storageFile;
            }
            else
            {
                string storage = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string storageFolder = Path.Combine(storage, "ai");
                string storageFile = Path.Combine(storageFolder, "models.json");
                return storageFile;
            }
        }

        
    }

}