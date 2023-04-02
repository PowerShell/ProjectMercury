// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Runspaces;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
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
        const string OPENAI_COMPLETION_URL = "https://powershell-openai.openai.azure.com/openai/deployments/gpt-35-turbo/chat/completions?api-version=2023-03-15-preview";
        private static HttpClient _httpClient;
        private static SecureString _openaiKey;
        private static System.Management.Automation.PowerShell _pwsh;
        private static List<string> _history = new();
        private static int _maxHistory = 256;
        private static List<string> _promptHistory = new();
        private static List<string> _assistHistory = new();
        private static StringBuilder _buffer = new();
        private static int _maxBuffer = 4096;

        [Parameter(Mandatory = false)]
        public SwitchParameter LastError { get; set; }

        public EnterCoPlot()
        {
            _pwsh = System.Management.Automation.PowerShell.Create();
            _pwsh.Runspace = Runspace.DefaultRunspace;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        protected override void BeginProcessing()
        {
            if (_openaiKey is null)
            {
                _openaiKey = GetApiKey();
                _httpClient.DefaultRequestHeaders.Add("api-key", ConvertSecureString(_openaiKey));
            }

            try
            {
                Console.Write(ALTERNATE_SCREEN_BUFFER);
                if (_buffer.Length > 0)
                {
                    Console.Write(_buffer.ToString());
                }
                else
                {
                    Console.CursorTop = Console.WindowHeight - 1;
                }

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
            WriteLineConsole(INSTRUCTIONS);
            var promptLength = (new StringDecorated(PROMPT)).ContentLength;
            var inputBuilder = new StringBuilder();
            while (!exit)
            {
                var historyIndex = _history.Count - 1;
                inputBuilder.Clear();
                WriteConsole(PROMPT);

                bool inputReceived = false;
                while (!inputReceived && !exit)
                {
                    var keyInfo = Console.ReadKey(true);
                    switch (keyInfo)
                    {
                        // up arrow
                        case ConsoleKeyInfo { Key: ConsoleKey.UpArrow, Modifiers: 0 }:
                            if (_history.Count > 0)
                            {
                                historyIndex--;
                                if (historyIndex < 0)
                                {
                                    historyIndex = 0;
                                }

                                Console.CursorLeft = promptLength;
                                Console.Write(" ".PadRight(Console.WindowWidth - promptLength));
                                Console.CursorLeft = promptLength;
                                Console.Write(_history[historyIndex]);
                                inputBuilder.Clear();
                                inputBuilder.Append(_history[historyIndex]);
                            }
                            break;
                        // down arrow
                        case ConsoleKeyInfo { Key: ConsoleKey.DownArrow, Modifiers: 0 }:
                            if (_history.Count > 0)
                            {
                                historyIndex++;
                                if (historyIndex >= _history.Count)
                                {
                                    historyIndex = _history.Count - 1;
                                }

                                Console.CursorLeft = promptLength;
                                Console.Write(" ".PadRight(Console.WindowWidth - promptLength));
                                Console.CursorLeft = promptLength;
                                Console.Write(_history[historyIndex]);
                                inputBuilder.Clear();
                                inputBuilder.Append(_history[historyIndex]);
                            }
                            break;
                        // enter
                        case ConsoleKeyInfo { Key: ConsoleKey.Enter, Modifiers: 0 }:
                            WriteLineConsole("");
                            if (inputBuilder.Length > 0)
                            {
                                _history.Add(inputBuilder.ToString());
                                if (_history.Count > _maxHistory)
                                {
                                    _history.RemoveAt(0);
                                }
                            }
                            inputReceived = true;
                            WriteLineBuffer(PROMPT + inputBuilder.ToString());
                            break;
                        // backspace
                        case ConsoleKeyInfo { Key: ConsoleKey.Backspace, Modifiers: 0 }:
                            if (inputBuilder.Length > 0)
                            {
                                inputBuilder.Remove(inputBuilder.Length - 1, 1);
                                Console.CursorLeft = promptLength + inputBuilder.Length;
                                Console.Write(" ");
                                Console.CursorLeft = promptLength + inputBuilder.Length;
                            }
                            break;
                        // ctrl+c
                        case ConsoleKeyInfo { Key: ConsoleKey.C, Modifiers: ConsoleModifiers.Control }:
                            return;
                        // left arrow
                        case ConsoleKeyInfo { Key: ConsoleKey.LeftArrow, Modifiers: 0 }:
                            if (Console.CursorLeft > promptLength)
                            {
                                Console.CursorLeft--;
                            }
                            break;
                        // right arrow
                        case ConsoleKeyInfo { Key: ConsoleKey.RightArrow, Modifiers: 0 }:
                            if (Console.CursorLeft < promptLength + inputBuilder.Length)
                            {
                                Console.CursorLeft++;
                            }
                            break;
                        // ctrl+u to erase line
                        case ConsoleKeyInfo { Key: ConsoleKey.U, Modifiers: ConsoleModifiers.Control }:
                            Console.CursorLeft = promptLength;
                            Console.Write(" ".PadRight(Console.WindowWidth - promptLength));
                            Console.CursorLeft = promptLength;
                            inputBuilder.Clear();
                            break;
                        default:
                            if (keyInfo.KeyChar != '\0')
                            {
                                inputBuilder.Append(keyInfo.KeyChar);
                                Console.Write(keyInfo.KeyChar);
                            }
                            break;
                    }
                }

                string input = inputBuilder.ToString();
                var cancelSource = new CancellationTokenSource();
                var cancelToken = cancelSource.Token;
                switch (input)
                {
                    case "help":
                        WriteLineConsole($"{PSStyle.Instance.Foreground.BrightCyan}Just type whatever you want to send to CoPilot.");
                        WriteLineConsole($"{PSStyle.Instance.Underline}Up{PSStyle.Instance.UnderlineOff} and {PSStyle.Instance.Underline}down{PSStyle.Instance.UnderlineOff} arrows will cycle through your history.");
                        WriteLineConsole($"{PSStyle.Instance.Underline}Ctrl+u{PSStyle.Instance.UnderlineOff} will clear the current line.");
                        WriteLineConsole($"Type {PSStyle.Instance.Underline}exit{PSStyle.Instance.UnderlineOff} to exit the chat.");
                        WriteLineConsole($"Type {PSStyle.Instance.Underline}clear{PSStyle.Instance.UnderlineOff} to clear the screen.");
                        break;
                    case "clear":
                        Console.Clear();
                        _buffer.Clear();
                        Console.CursorTop = Console.WindowHeight - 1;
                        break;
                    case "debug":
                        debug = !debug;
                        WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Debug mode is now {(debug ? "on" : "off")}.");
                        WriteLineConsole($"PID: {Environment.ProcessId}");
                        break;
                    case "get-error":
                        var lastError = GetLastError();
                        WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Last error: {lastError}");
                        // TODO: send to AI
                        break;
                    case "history":
                        foreach (var item in _history)
                        {
                            WriteLineConsole(item);
                        }
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
                                        WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}Task was cancelled.");
                                        taskCompleted = true;
                                        break;
                                    case TaskStatus.Faulted:
                                        WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}Task faulted.");
                                        taskCompleted = true;
                                        break;
                                    default:
                                        break;
                                }

                                Console.CursorTop = cursorTop;
                                Console.CursorLeft = 0;
                                Console.Write($"{task.Status}... {SPINNER[i++ % SPINNER.Length]}".PadRight(Console.WindowWidth));
                                if (Console.KeyAvailable)
                                {
                                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                                    if (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers == ConsoleModifiers.Control)
                                    {
                                        cancelSource.Cancel();
                                        break;
                                    }
                                }
                                System.Threading.Thread.Sleep(100);
                            }
                            Console.CursorTop = cursorTop;
                            Console.CursorLeft = 0;
                            Console.WriteLine(" ".PadRight(Console.WindowWidth));
                            var output = task.Result;
                            _assistHistory.Add(output);
                            _promptHistory.Add(input);
                            if (_assistHistory.Count > _maxHistory)
                            {
                                _assistHistory.RemoveAt(0);
                                _promptHistory.RemoveAt(0);
                            }

                            WriteLineConsole($"{PSStyle.Instance.Background.FromRgb(20, 0, 20)}{PSStyle.Instance.Foreground.BrightYellow}{output}{PSStyle.Instance.Reset}\n");
                        }
                        catch (Exception e)
                        {
                            WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}EXCEPTION: {e.Message}\n{e.StackTrace}");
                        }
                        break;
                }
            }
        }

        private void WriteConsole(string text)
        {
            AddToBuffer(text);
            Console.Write(text);
        }

        private void WriteLineConsole(string text)
        {
            AddToBuffer(text);
            Console.WriteLine(text);
        }

        private void WriteLineBuffer(string text)
        {
            AddToBuffer(text);
            AddToBuffer("\n");
        }

        private void AddToBuffer(string text)
        {
            _buffer.Append(text);
            if (_buffer.Length > _maxBuffer)
            {
                _buffer.Remove(0, _buffer.Length - _maxBuffer);
            }
        }

        private string GetLastError()
        {
            _pwsh.AddCommand("Get-Error").AddParameter("Newest", 1);
            _pwsh.AddCommand("Out-String");
            var result = _pwsh.Invoke<string>();
            var sb = new StringBuilder();
            foreach (var item in result)
            {
                sb.AppendLine(item);
            }

            return sb.ToString();
        }

        private static string GetCompletion(string prompt, bool debug, CancellationToken cancelToken)
        {
            try
            {
                var requestBody = GetRequestBody(prompt);
                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: RequestBody:\n{GetPrettyJson(requestBody)}");
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
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: ResponseContent:\n{GetPrettyJson(responseContent)}");
                }
                var responseJson = JsonNode.Parse(responseContent);
                var output = responseJson!["choices"][0]["message"]["content"].ToString();
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

        private static string GetPrettyJson(string json)
        {
            using var jDoc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
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
            var messages = new List<object>();
            messages.Add(
                new
                {
                    role = "system",
                    content = "You are an AI assistant with experise in PowerShell and the command line. You are helpful, creative, clever, and very friendly."
                }
            );
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
                max_tokens = 800,
                frequency_penalty = 0,
                presence_penalty = 0,
                top_p = 0.95,
                temperature = 0.7,
                stop = (string)null
            };

            return JsonSerializer.Serialize(requestBody);
        }
    }
}
