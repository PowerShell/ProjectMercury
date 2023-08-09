using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerShell.Copilot
{
    public class History
    {
        public List<ModelHistory> historyList {get; set;}

        public History()
        {
            historyList = new List<ModelHistory>();
        }
    }

    public class ModelHistory
    {
        public required string Name {get; set;}
        public List<string>? History {get; set;}
    }

    public class HistoryFunctions
    {
        internal static void addToHistory(string input)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string filepath = Path.Combine(currentDirectory, "history" + Program.GetParentProcessID() + ".json");
            if(File.Exists(filepath))
            {
                string jsonString = File.ReadAllText(filepath);
                History history = JsonSerializer.Deserialize<History>(jsonString)!;
                string? activeModel = ModelFunctions.getCurrentModel()?.Name;

                ModelHistory? foundModel = history.historyList.Find(Model => Model.Name.ToLower() == activeModel?.ToLower());
                foundModel?.History?.Add(input);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedHistory = JsonSerializer.Serialize(history, options);
                File.WriteAllText(filepath, updatedHistory);
            }
            else
            {
                List<string> newEntry = new List<string>();
                newEntry.Add(input);
                var history = new History();
                string? activeModel = ModelFunctions.getCurrentModel()?.Name;

                if(activeModel != null)
                {
                    ModelHistory modelHistory = new ModelHistory
                    {
                        Name = activeModel,
                        History = newEntry
                    };
                    history.historyList?.Add(modelHistory);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string updatedHistory = JsonSerializer.Serialize(history, options);
                    File.WriteAllText(filepath, updatedHistory);
                }   
            }
        }

        internal static List<string>? getHistory()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string filepath = Path.Combine(currentDirectory, "history" + Program.GetParentProcessID() + ".json");
            if(File.Exists(filepath))
            {
                string jsonString = File.ReadAllText(filepath);
                History history = JsonSerializer.Deserialize<History>(jsonString)!;
                string? activeModel = ModelFunctions.getCurrentModel()?.Name;
                ModelHistory? foundModel = history.historyList.Find(Model => Model.Name.ToLower() == activeModel?.ToLower());

                return foundModel?.History;
            }
            return null;
        }

        internal static void addModelToHistory(string model)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string filepath = Path.Combine(currentDirectory, "history" + Program.GetParentProcessID() + ".json");
            if(File.Exists(filepath))
            {
                string jsonString = File.ReadAllText(filepath);
                History history = JsonSerializer.Deserialize<History>(jsonString)!;
                ModelHistory newModelHistory = new ModelHistory()
                {
                    Name = model,
                    History = new List<string>()
                };
                history.historyList?.Add(newModelHistory);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedHistory = JsonSerializer.Serialize(history, options);
                File.WriteAllText(filepath, updatedHistory);
            }
            else
            {
                var history = new History();
                ModelHistory modelHistory = new ModelHistory
                {
                    Name = model,
                    History = new List<string>()
                };
                history.historyList?.Add(modelHistory);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedHistory = JsonSerializer.Serialize(history, options);
                File.WriteAllText(filepath, updatedHistory);  
            }
        }

        internal static void clearHistory()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string filepath = Path.Combine(currentDirectory, "history" + Program.GetParentProcessID() + ".json");
            if(File.Exists(filepath))
            {
                string jsonString = File.ReadAllText(filepath);
                string? activeModel = ModelFunctions.getCurrentModel()?.Name;
                History history = JsonSerializer.Deserialize<History>(jsonString)!;
                ModelHistory? foundModel = history.historyList.Find(Model => Model.Name.ToLower() == activeModel?.ToLower());
                if(foundModel != null)
                {
                    foundModel.History = new List<string>();
                }
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedHistory = JsonSerializer.Serialize(history, options);
                File.WriteAllText(filepath, updatedHistory);

                FileInfo fileInfo = new FileInfo(filepath);
                if(fileInfo.Length == 0)
                {
                    try
                    {
                        File.Delete(filepath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting history file: {ex.Message}");
                    }
                }
            }
            
        }

        internal static void printHistory(bool print)
        {
            if(print)
            {
                string currentDirectory = Directory.GetCurrentDirectory();
                string filepath = Path.Combine(currentDirectory, "history" + Program.GetParentProcessID() + ".json");
                //string contents = File.ReadAllText(filepath);
                Screenbuffer.WriteConsole($"{Screenbuffer.RESET}");
                if(File.Exists(filepath))
                {
                    string jsonString = File.ReadAllText(filepath);
                    string? activeModel = ModelFunctions.getCurrentModel()?.Name;
                    History history = JsonSerializer.Deserialize<History>(jsonString)!;
                    ModelHistory? foundModel = history.historyList.Find(Model => Model.Name.ToLower() == activeModel?.ToLower());
                    if(foundModel != null)
                    {
                        for(int i = 0; i < foundModel.History?.Count; i++)
                        {
                            if(i % 2 == 0)
                            {
                                Screenbuffer.WriteConsole("\n" + Readline.PROMPT);
                                Screenbuffer.WriteLineConsole(foundModel.History[i].ToString().TrimEnd());
                            }
                            else
                            {
                                var colorOutput = new StringBuilder();
                                colorOutput.AppendLine($"{PSStyle.Instance.Foreground.BrightYellow}{foundModel.History[i]}");
                                Screenbuffer.WriteConsole($"{colorOutput.ToString()}{Screenbuffer.RESET}");
                            }
                        }
                    }
                    
                }
            }
        }
    }
}