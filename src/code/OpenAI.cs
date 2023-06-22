using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure;
using Azure.Core;

namespace Microsoft.PowerShell.Copilot
{
    internal class OpenAI
    {
        private const string API_ENV_VAR = "AZURE_OPENAI_API_KEY";
        internal const string ENDPOINT_ENV_VAR = "AZURE_OPENAI_ENDPOINT";
        internal const string SYSTEM_PROMPT_ENV_VAR = "AZURE_OPENAI_SYSTEM_PROMPT";


        private static readonly string[] SPINNER = new string[8] {"🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘"};
        private static List<string> _promptHistory = new();
        private static List<string> _assistHistory = new();
        private static int _maxHistory = 256;
        internal static string _lastCodeSnippet = string.Empty;
        OpenAIClient client;

        string endpoint;
        private static string _os = GetOS();

        public OpenAI()
        {
            endpoint = Environment.GetEnvironmentVariable(ENDPOINT_ENV_VAR);
            if(endpoint is null)
            {
                endpoint = "https://pscopilot.azure-api.net";
            }

            OpenAIClientOptions options = new OpenAIClientOptions();
            options.Retry.MaxRetries = 0;

            string apiKey = Environment.GetEnvironmentVariable(API_ENV_VAR);
            if (apiKey is null)
            {
                throw(new Exception($"{API_ENV_VAR} environment variable not set"));
            }


            if (endpoint.EndsWith(".azure-api.net", StringComparison.Ordinal) || endpoint.EndsWith(".azure-api.net/", StringComparison.Ordinal))
            {
                AzureKeyCredentialPolicy policy = new AzureKeyCredentialPolicy(new AzureKeyCredential(apiKey), "Ocp-Apim-Subscription-Key");
                options.AddPolicy(policy, Azure.Core.HttpPipelinePosition.PerRetry);

                client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential("placeholder"), options);
            }
            else if (endpoint.EndsWith(".openai.azure.com", StringComparison.Ordinal) || endpoint.EndsWith(".openai.azure.com/", StringComparison.Ordinal))
            {
                client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            }
            else
            {
                throw new Exception($"The specified endpoint '{endpoint}' is not a valid Azure OpenAI service endpoint.");
            }
        }

        internal string LastCodeSnippet()
        {
            return _lastCodeSnippet;
        }

        internal void SendPrompt(string input, bool debug, CancellationToken cancelToken)
        {
            try
            {
                var task = new Task<string>(() =>
                {
                    return GetCompletion(input, debug, cancelToken);
                });
                task.Start();
                int i = 0;
                int cursorTop = Console.CursorTop;
                bool taskCompleted = false;
                while (!taskCompleted && task.Status != TaskStatus.RanToCompletion)
                {
                    switch (task.Status)
                    {
                        case TaskStatus.Canceled:
                            Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}Task was cancelled.");
                            taskCompleted = true;
                            break;
                        case TaskStatus.Faulted:
                            Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}Task faulted.");
                            taskCompleted = true;
                            break;
                        default:
                            break;
                    }

                    Console.CursorTop = cursorTop;
                    Console.CursorLeft = 0;
                    Console.CursorVisible = false;
                    Console.Write($"{Screenbuffer.RESET}{task.Status}... {SPINNER[i++ % SPINNER.Length]}".PadRight(Console.WindowWidth));
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                        if (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers == ConsoleModifiers.Control)
                        {
                            EnterCopilot.Cancel();
                            Console.CursorVisible = true;
                            break;
                        }
                    }
                    System.Threading.Thread.Sleep(100);
                }
                Console.CursorTop = cursorTop;
                Console.CursorLeft = 0;
                Console.Write(" ".PadRight(Console.WindowWidth));
                Console.CursorVisible = true;
                var output = task.Result;
                _assistHistory.Add(output);
                _promptHistory.Add(input);
                if (_assistHistory.Count > _maxHistory)
                {
                    _assistHistory.RemoveAt(0);
                    _promptHistory.RemoveAt(0);
                }

                GetCodeSnippet(output);

                // colorize code sections
                // split into lines
                var lines = output.Split(new[] { '\n' });
                var colorOutput = new StringBuilder();
                var codeSnippet = new StringBuilder();
                bool inCode = false;
                foreach (var line in lines)
                {
                    if (line.StartsWith("```powershell"))
                    {
                        inCode = true;
                        colorOutput.AppendLine($"{PSStyle.Instance.Foreground.BrightBlack}```");
                    }
                    else if (line.StartsWith("```"))
                    {
                        inCode = false;
                        colorOutput.Append(Formatting.GetPrettyPowerShellScript(codeSnippet.ToString()));
                        codeSnippet.Clear();
                        colorOutput.AppendLine($"{PSStyle.Instance.Foreground.BrightBlack}```");
                    }
                    else if (inCode)
                    {
                        codeSnippet.AppendLine(line);
                    }
                    else
                    {
                        colorOutput.AppendLine($"{PSStyle.Instance.Foreground.BrightYellow}{line}");
                    }
                }

                Screenbuffer.WriteConsole($"{colorOutput.ToString()}{Screenbuffer.RESET}");
            }
            catch (Exception e)
            {
                Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}EXCEPTION: {e.Message}\n{e.StackTrace}");
            }
        }
        internal string GetCompletion(string prompt, bool debug, CancellationToken cancelToken)
        {
            try
            {
                ChatCompletionsOptions requestBody = GetRequestBody(prompt);
                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: RequestBody:\n{Formatting.GetPrettyJson(requestBody.ToString())}");
                }

                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: OpenAI URL: {endpoint}");
                }

                string openai_model = "";
                switch (EnterCopilot._model)
                {
                    case Model.GPT35_Turbo:
                        openai_model = "gpt-35-turbo";
                        break;
                    case Model.GPT4_32K:
                        openai_model = "gpt4-32k";
                        break;
                    default:
                        openai_model = "gpt4";
                        break;
                }

                Response<ChatCompletions> response = client.GetChatCompletions(
                deploymentOrModelName: openai_model,
                requestBody);

                ChatCompletions chatCompletions = response.Value;
                var output = "\n";

                output += chatCompletions.Choices[0].Message.Content;

                return output;
                
            }
            catch (RequestFailedException e)
            {
                return $"{PSStyle.Instance.Foreground.BrightRed}HTTP EXCEPTION: {e.Message}";
            }
            catch (OperationCanceledException)
            {
                return $"{PSStyle.Instance.Foreground.BrightRed}Operation cancelled.";
            }
            catch (Exception e)
            {
                return $"{PSStyle.Instance.Foreground.BrightRed}HTTP EXCEPTION: {e.Message}\n{e.StackTrace}";
            }
        }

        private static string GetOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "macos";
            }
            else
            {
                return "unknown";
            }
        }

        private static ChatCompletionsOptions GetRequestBody(string prompt)
        {
            ChatCompletionsOptions requestBody = new ChatCompletionsOptions();
            var messages = requestBody.Messages;
            string system_prompt = Environment.GetEnvironmentVariable(SYSTEM_PROMPT_ENV_VAR);
            if (system_prompt is null)
            {
                messages.Add(
                    new ChatMessage
                    (
                        role: "system",
                        content: $"You are an AI assistant with experise in PowerShell, Azure, and the command line.  Assume user is using {_os} operating system unless specified. You are helpful, creative, clever, and very friendly. Responses including PowerShell code are enclosed in ```powershell blocks."
                    )
                );
            }
            else
            {
                messages.Add(
                    new ChatMessage
                    (
                        role: "system",
                        content: system_prompt
                    )
                );
            }

            for (int i = 0; i < _assistHistory.Count; i++)
            {
                messages.Add(
                    new ChatMessage
                    (
                        role: "user",
                        content: _promptHistory[i]
                    )
                );
                messages.Add(
                    new ChatMessage
                    (
                        role: "assistant",
                        content: _assistHistory[i]
                    )
                );
            }

            messages.Add(
                new ChatMessage
                (
                    role: "user",
                    content: prompt
                )
            );

            requestBody.MaxTokens = 300;
            requestBody.Temperature = (float?) 0.7;
            return requestBody;
        }

        private static void GetCodeSnippet(string input)
        {
            // split input into lines
            var lines = input.Split(new[] { '\n' });
            var codeSnippet = new StringBuilder();
            // find the first line that starts with ```powershell and copy the lines until ``` is found
            // TODO: handle case where there isn't a PowerShell but just a command-line block
            bool foundStart = false;
            bool foundEnd = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("```powershell"))
                {
                    foundStart = true;
                }
                else if (line.StartsWith("```"))
                {
                    foundEnd = true;
                }
                else if (foundStart && !foundEnd)
                {
                    codeSnippet.AppendLine(line);
                }
            }

            _lastCodeSnippet = codeSnippet.ToString();
        }
        
    }
}
