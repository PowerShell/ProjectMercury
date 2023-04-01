// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.CoPilot
{
    [Alias("wtf")]
    [Cmdlet(VerbsDiagnostic.Resolve, "Error")]
    public sealed class ResolveError : PSCmdlet
    {
    }

    [Alias("copilot")]
    [Cmdlet(VerbsCommon.Enter, "CoPilot")]
    public sealed class EnterCoPlot : PSCmdlet
    {
        const string ALTERNATE_SCREEN_BUFFER = "\x1b[?1049h";
        const string MAIN_SCREEN_BUFFER = "\x1b[?1049l";
        const string PROMPT = "\x1b[0;1;32mCoPilot> \x1b[0m";
        const string MODEL = "gpt-35-turbo";
        const string SPINNER = "|/-\\";
        const int MAX_TOKENS = 64;
        const string API_ENV_VAR = "AZURE_OPENAI_API_KEY";
        const string INSTRUCTIONS = "\x1b[0mType 'help' for instructions.";
        const string OPENAI_COMPLETION_URL = "https://powershell-openai.openai.azure.com/openai/deployments/gpt-35-turbo/completions?api-version=2022-12-01";
        private static HttpClient _httpClient = new();
        private static SecureString _openaiKey;
        private static System.Management.Automation.PowerShell _pwsh;
        private List<string> _history = new();

        [Parameter(Mandatory = false)]
        public SwitchParameter LastError { get; set; }

        protected override void BeginProcessing()
        {
            if (_pwsh is null)
            {
                _pwsh = System.Management.Automation.PowerShell.Create();
            }

            if (_openaiKey is null)
            {
                _openaiKey = GetApiKey();
            }

            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("api-key", ConvertSecureString(_openaiKey));

            try
            {
                Console.Write(ALTERNATE_SCREEN_BUFFER);
                // put cursor on bottom line
                Console.CursorTop = Console.WindowHeight - 1;
                EnterInputLoop();
            }
            finally
            {
                Console.Write(MAIN_SCREEN_BUFFER);
            }
        }

        private void EnterInputLoop()
        {
            bool debug = false;
            bool exit = false;
            Console.TreatControlCAsInput = true;
            Console.WriteLine(INSTRUCTIONS);
            while (!exit)
            {
                Console.Write(PROMPT);
                string input = Console.ReadLine();
                var cancelSource = new CancellationTokenSource();
                var cancelToken = cancelSource.Token;
                switch (input)
                {
                    case "help":
                        Console.WriteLine($"{PSStyle.Instance.Foreground.BrightCyan}Just type whatever you want to send to CoPilot.");
                        Console.WriteLine("Type 'exit' to exit the chat.");
                        Console.WriteLine("Type 'clear' to clear the screen.");
                        break;
                    case "clear":
                        Console.Clear();
                        Console.CursorTop = Console.WindowHeight - 1;
                        break;
                    case "debug":
                        debug = !debug;
                        Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}Debug mode is now {(debug ? "on" : "off")}.");
                        Console.WriteLine($"PID: {Environment.ProcessId}");
                        break;
                    case "get-error":
                        var lastError = GetLastError();
                        Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}Last error: {lastError}");
                        // TODO: send to AI
                        break;
                    case "exit":
                        exit = true;
                        break;
                    default:
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
                                        Console.WriteLine($"{PSStyle.Instance.Foreground.BrightRed}Task was cancelled.");
                                        taskCompleted = true;
                                        break;
                                    case TaskStatus.Faulted:
                                        Console.WriteLine($"{PSStyle.Instance.Foreground.BrightRed}Task faulted.");
                                        taskCompleted = true;
                                        break;
                                    default:
                                        break;
                                }

                                Console.CursorTop = cursorTop;
                                Console.CursorLeft = 0;
                                Console.Write($"{task.Status}... {SPINNER[i++ % SPINNER.Length]}");
                                // see if ctrl+c was pressed
                                if (Console.KeyAvailable)
                                {
                                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                                    if (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers == ConsoleModifiers.Control)
                                    {
                                        cancelSource.Cancel();
                                        Console.WriteLine($"{PSStyle.Instance.Foreground.BrightRed}Task cancelled.");
                                        break;
                                    }
                                }
                                System.Threading.Thread.Sleep(100);
                            }
                            Console.CursorTop = cursorTop;
                            Console.CursorLeft = 0;
                            Console.WriteLine(" ".PadRight(Console.WindowWidth));
                            var output = task.Result;
                            Console.WriteLine(output);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"{PSStyle.Instance.Foreground.BrightRed}EXCEPTION: {e.Message}\n{e.StackTrace}");
                        }
                        break;
                }
            }
        }

        private string GetLastError()
        {
            _pwsh.AddCommand("Get-Error");
            _pwsh.AddParameter("Newest", 1);
            _pwsh.AddCommand("Out-String");
            var result = _pwsh.Invoke();
            return result[0].ToString();
        }

        private static string GetCompletion(string prompt, bool debug, CancellationToken cancelToken)
        {
            try
            {
                var requestBody = GetRequestBody(prompt);
                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: RequestBody: {requestBody}");
                }

                var bodyContent = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
                var response = _httpClient.PostAsync(OPENAI_COMPLETION_URL, bodyContent, cancelToken).GetAwaiter().GetResult();
                var responseContent = response.Content.ReadAsStringAsync(cancelToken).GetAwaiter().GetResult();
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return $"{PSStyle.Instance.Foreground.BrightRed}ERROR: {responseContent}";
                }

                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: ResponseContent: {responseContent}");
                }
                var responseJson = JsonNode.Parse(responseContent);
                var output = responseJson!["choices"][0]["text"].ToString();
                return $"{PSStyle.Instance.Background.FromRgb(20,0,20)}{PSStyle.Instance.Foreground.BrightGreen}{output}{PSStyle.Instance.Reset}\n";
            }
            catch (Exception e)
            {
                return $"{PSStyle.Instance.Foreground.BrightRed}HTTP EXCEPTION: {e.Message}\n{e.StackTrace}";
            }
        }

        private SecureString GetApiKey()
        {
            string key = Environment.GetEnvironmentVariable(API_ENV_VAR);
            if (key is null)
            {
                ThrowTerminatingError(
                    new ErrorRecord(
                        new InvalidOperationException($"{API_ENV_VAR} environment variable not set"),
                        "AzureOpenAIKeyNotFound",
                        ErrorCategory.InvalidOperation,
                        targetObject: null));
                return null;
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

        private static string GetRequestBody(string prompt)
        {
            var promptMessage = $"The following is a conversation with an AI assistant with experise in PowerShell and the command line. The assistant is helpful, creative, clever, and very friendly.\n\nHuman: {prompt}\nAI:";

            var requestBody = new
            {
                prompt = promptMessage,
                max_tokens = 256,
                frequency_penalty = 0,
                presence_penalty = 0,
                top_p = 1,
                temperature = 0.9,
                stop = new string[] { "Human:", "AI:" }
            };

            return JsonSerializer.Serialize(requestBody);
        }
    }
}
