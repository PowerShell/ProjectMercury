using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.Copilot
{
    internal class OpenAI
    {
        private const string API_ENV_VAR = "AZURE_OPENAI_API_KEY";
        internal const string ENDPOINT_ENV_VAR = "AZURE_OPENAI_ENDPOINT";
        internal const string SYSTEM_PROMPT_ENV_VAR = "AZURE_OPENAI_SYSTEM_PROMPT";
        private const string OPENAI_GPT35_TURBO_URL = "https://powershell-openai.openai.azure.com/openai/deployments/gpt-35-turbo/chat/completions?api-version=2023-03-15-preview";
        private const string OPENAI_GPT4_URL = "https://powershell-openai.openai.azure.com/openai/deployments/gpt4/chat/completions?api-version=2023-03-15-preview";
        private const string OPENAI_GPT4_32K_URL = "https://powershell-openai.openai.azure.com/openai/deployments/gpt4-32k/chat/completions?api-version=2023-03-15-preview";
        private static SecureString _openaiKey;

        private static readonly string[] SPINNER = new string[8] {"🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘"};
        private static List<string> _promptHistory = new();
        private static List<string> _assistHistory = new();
        private static int _maxHistory = 256;
        internal static string _lastCodeSnippet = string.Empty;
        private static HttpClient _httpClient = new();
        private static string _os = GetOS();

        public OpenAI()
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            if (_openaiKey is null)
            {
                _openaiKey = GetApiKey();
                _httpClient.DefaultRequestHeaders.Add("api-key", ConvertSecureString(_openaiKey));
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
                var requestBody = GetRequestBody(prompt);
                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: RequestBody:\n{Formatting.GetPrettyJson(requestBody)}");
                }

                var bodyContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
                string openai_url = Environment.GetEnvironmentVariable(ENDPOINT_ENV_VAR);
                if (openai_url is null)
                {
                    switch (EnterCopilot._model)
                    {
                        case Model.GPT35_Turbo:
                            openai_url = OPENAI_GPT35_TURBO_URL;
                            break;
                        case Model.GPT4:
                            openai_url = OPENAI_GPT4_URL;
                            break;
                        case Model.GPT4_32K:
                            openai_url = OPENAI_GPT4_32K_URL;
                            break;
                        default:
                            openai_url = OPENAI_GPT4_URL;
                            break;
                    }
                }

                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: OpenAI URL: {openai_url}");
                }

                var response = _httpClient.PostAsync(openai_url , bodyContent, cancelToken).GetAwaiter().GetResult();
                var responseContent = response.Content.ReadAsStringAsync(cancelToken).GetAwaiter().GetResult();
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return $"{PSStyle.Instance.Foreground.BrightRed}ERROR: {responseContent}";
                }

                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: ResponseContent:\n{Formatting.GetPrettyJson(responseContent)}");
                }

                var responseJson = JsonNode.Parse(responseContent);
                var output = "\n" + responseJson!["choices"][0]["message"]["content"].ToString();
                return output;
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

        private static string GetRequestBody(string prompt)
        {
            var messages = new List<object>();
            string system_prompt = Environment.GetEnvironmentVariable(SYSTEM_PROMPT_ENV_VAR);
            if (system_prompt is null)
            {
                messages.Add(
                    new
                    {
                        role = "system",
                        content = $"You are an AI assistant with experise in PowerShell, Azure, and the command line.  Assume user is using {_os} operating system unless specified. You are helpful, creative, clever, and very friendly. Responses including PowerShell code are enclosed in ```powershell blocks."
                    }
                );
            }
            else
            {
                messages.Add(
                    new
                    {
                        role = "system",
                        content = system_prompt
                    }
                );
            }

            for (int i = 0; i < _assistHistory.Count; i++)
            {
                messages.Add(
                    new
                    {
                        role = "user",
                        content = _promptHistory[i]
                    }
                );
                messages.Add(
                    new
                    {
                        role = "assistant",
                        content = _assistHistory[i]
                    }
                );
            }

            messages.Add(
                new
                {
                    role = "user",
                    content = prompt
                }
            );

            var requestBody = new
            {
                messages = messages,
                max_tokens = 4096,
                frequency_penalty = 0,
                presence_penalty = 0,
                top_p = 0.95,
                temperature = 0.7,
                stop = (string)null
            };

            return JsonSerializer.Serialize(requestBody);
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

        private SecureString GetApiKey()
        {
            string key = Environment.GetEnvironmentVariable(API_ENV_VAR);
            if (key is null)
            {
                throw(new Exception($"{API_ENV_VAR} environment variable not set"));
            }
            else
            {
                var ss = new SecureString();
                foreach (char c in key)
                {
                    ss.AppendChar(c);
                }

                return ss;
            }
        }

        private static string ConvertSecureString(SecureString ss)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(ss);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}
