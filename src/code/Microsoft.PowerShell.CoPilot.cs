// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Language;
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
    public enum Model
    {
        GPT35_Turbo,
        GPT4,
    }

    [Alias("Copilot")]
    [Cmdlet(VerbsCommon.Enter, "Copilot")]
    public sealed class EnterCoPlot : PSCmdlet
    {
        private const char ESC = '\x1b';
        private readonly string RESET = $"{PSStyle.Instance.Reset}{PSStyle.Instance.Background.FromRgb(20, 0, 20)}";
        private const string ALTERNATE_SCREEN_BUFFER = "\x1b[?1049h";
        private const string MAIN_SCREEN_BUFFER = "\x1b[?1049l";
        private readonly string PROMPT = $"{PSStyle.Instance.Foreground.BrightGreen}Copilot> {PSStyle.Instance.Foreground.White}";
        private const string MODEL = "gpt-35-turbo";
        private static string[] SPINNER = new string[8] {"🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘"};
        private const int MAX_TOKENS = 64;
        private const string API_ENV_VAR = "AZURE_OPENAI_API_KEY";
        private readonly string INSTRUCTIONS = $"{PSStyle.Instance.Foreground.Cyan}Type 'help' for instructions.";
        private const string OPENAI_GPT35_TURBO_URL = "https://powershell-openai.openai.azure.com/openai/deployments/gpt-35-turbo/chat/completions?api-version=2023-03-15-preview";
        private const string OPENAI_GPT4_URL = "https://powershell-openai.openai.azure.com/openai/deployments/gpt4/chat/completions?api-version=2023-03-15-preview";
        private const string LOGO = @"

██████╗ ███████╗ ██████╗ ██████╗ ██████╗ ██╗██╗      ██████╗ ████████╗
██╔══██╗██╔════╝██╔════╝██╔═══██╗██╔══██╗██║██║     ██╔═══██╗╚══██╔══╝
██████╔╝███████╗██║     ██║   ██║██████╔╝██║██║     ██║   ██║   ██║
██╔═══╝ ╚════██║██║     ██║   ██║██╔═══╝ ██║██║     ██║   ██║   ██║
██║     ███████║╚██████╗╚██████╔╝██║     ██║███████╗╚██████╔╝   ██║
╚═╝     ╚══════╝ ╚═════╝ ╚═════╝ ╚═╝     ╚═╝╚══════╝ ╚═════╝    ╚═╝   v0.1
";
        private static HttpClient _httpClient = new HttpClient();
        private static SecureString _openaiKey;
        private static System.Management.Automation.PowerShell _pwsh = System.Management.Automation.PowerShell.Create();
        private static List<string> _history = new();
        private static int _maxHistory = 256;
        private static List<string> _promptHistory = new();
        private static List<string> _assistHistory = new();
        private static StringBuilder _buffer = new();
        private static int _maxBuffer = 4096;
        private static CancellationTokenSource _cancellationTokenSource = new();
        private static CancellationToken _cancelToken = _cancellationTokenSource.Token;
        private readonly ConsoleKeyInfo _exitKeyInfo = GetPSReadLineKeyHandler();
        private static string _lastCodeSnippet = string.Empty;
        private static Model _model = Model.GPT35_Turbo;

        [Parameter(Mandatory = false)]
        public SwitchParameter LastError { get; set; }

        [Parameter(Mandatory = false)]
        public Model Model
        {
            get { return _model; }
            set { _model = value; }
        }

        public EnterCoPlot()
        {
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
                RedrawScreen();
                if (LastError)
                {
                    var input = GetLastError();
                    if (input.Length > 0)
                    {
                        WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Last error: {input}{RESET}");
                        SendPrompt(input, false, _cancelToken);
                    }
                }

                EnterInputLoop();
            }
            finally
            {
                Console.Write(MAIN_SCREEN_BUFFER);
            }
        }

        private void RedrawScreen()
        {
            Console.Write($"{RESET}");
            Console.Clear();
            // WriteToolbar();
            Console.CursorTop = Console.WindowHeight - 1;
            Console.CursorLeft = 0;
            if (_buffer.Length > 0)
            {
                Console.Write(_buffer.ToString());
            }
            else
            {
                WriteLineConsole($"{RESET}{LOGO}");
                WriteLineConsole($"{INSTRUCTIONS}");
            }
        }

        private void EnterInputLoop()
        {
            bool debug = false;
            bool exit = false;
            Console.TreatControlCAsInput = true;
            var promptLength = (new StringDecorated(PROMPT)).ContentLength;
            var inputBuilder = new StringBuilder();
            var consoleHeight = Console.WindowHeight;
            var consoleWidth = Console.WindowWidth;
            WriteLineConsole($"{PSStyle.Instance.Foreground.Yellow}Using {_model}");
            while (!exit)
            {
                var historyIndex = _history.Count - 1;
                inputBuilder.Clear();
                WriteLineConsole($"{RESET}");
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
                            if (inputBuilder.Length > 0)
                            {
                                _history.Add(inputBuilder.ToString());
                                if (_history.Count > _maxHistory)
                                {
                                    _history.RemoveAt(0);
                                }
                            }
                            inputReceived = true;
                            WriteLineBuffer(inputBuilder.ToString());
                            WriteLineConsole("");

                            // see if terminal was resized
                            if (consoleHeight != Console.WindowHeight || consoleWidth != Console.WindowWidth)
                            {
                                consoleHeight = Console.WindowHeight;
                                consoleWidth = Console.WindowWidth;
                                RedrawScreen();
                            }

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
                        // ctrl+c to copy to clipboard
                        case ConsoleKeyInfo { Key: ConsoleKey.C, Modifiers: ConsoleModifiers.Control }:
                            inputBuilder.Clear();
                            inputBuilder.Append("copy-code");
                            inputReceived = true;
                            WriteLineConsole(inputBuilder.ToString());
                            WriteLineConsole("");
                            break;
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
                        // ctrl+e to get error
                        case ConsoleKeyInfo { Key: ConsoleKey.E, Modifiers: ConsoleModifiers.Control }:
                            inputBuilder.Clear();
                            inputBuilder.Append("Get-Error");
                            inputReceived = true;
                            WriteLineConsole(inputBuilder.ToString());
                            WriteLineConsole("");
                            break;
                        default:
                            if (keyInfo == _exitKeyInfo)
                            {
                                // remove last line from buffer
                                var stringBuffer = _buffer.ToString();
                                var last = stringBuffer.LastIndexOf('\n');
                                if (last > 0)
                                {
                                    _buffer.Remove(last, _buffer.Length - last);
                                }

                                return;
                            }

                            if (keyInfo.KeyChar != '\0')
                            {
                                inputBuilder.Append(keyInfo.KeyChar);
                                Console.Write(keyInfo.KeyChar);
                            }
                            break;
                    }
                }

                string input = inputBuilder.ToString();
                switch (input.ToLowerInvariant())
                {
                    case "help":
                        var highlight = PSStyle.Instance.Underline + PSStyle.Instance.Foreground.BrightCyan + PSStyle.Instance.Bold;
                        var highlightOff = PSStyle.Instance.UnderlineOff + PSStyle.Instance.Foreground.Cyan + PSStyle.Instance.BoldOff;
                        WriteLineConsole($"\n{highlightOff}Just type whatever you want to send to Copilot.");
                        WriteLineConsole($"{highlight}Up{highlightOff} and {highlight}down{highlightOff} arrows will cycle through your history.");
                        WriteLineConsole($"{highlight}Ctrl+u{highlightOff} will clear the current line.");
                        WriteLineConsole($"{highlight}Ctrl+c{highlightOff} or {highlight}Copy-Code{highlightOff} will copy the current line to the clipboard.");
                        WriteLineConsole($"{highlight}Ctrl+e{highlightOff} or {highlight}Get-Error{highlightOff} will get the last error.");
                        WriteLineConsole($"Type {highlight}exit{highlightOff} to exit the chat.");
                        WriteLineConsole($"Type {highlight}clear{highlightOff} to clear the screen.");
                        break;
                    case "clear":
                        Console.Clear();
                        _buffer.Clear();
                        Console.CursorTop = Console.WindowHeight - 2;
                        break;
                    case "debug":
                        debug = !debug;
                        WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Debug mode is now {(debug ? "on" : "off")}.");
                        WriteLineConsole($"PID: {Environment.ProcessId}");
                        break;
                    case "history":
                        foreach (var item in _history)
                        {
                            WriteLineConsole(item);
                        }
                        break;
                    case "exit":
                        exit = true;
                        // remove last line from buffer
                        var stringBuffer = _buffer.ToString();
                        var last = stringBuffer.LastIndexOf('\n');
                        if (last > 0)
                        {
                            _buffer.Remove(last, _buffer.Length - last);
                        }
                        break;
                    case "copy-code":
                        if (_lastCodeSnippet.Length > 0)
                        {
                            CopyToClipboard(_lastCodeSnippet);
                            WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Code snippet copied to clipboard.{RESET}");
                        }
                        break;
                    case "get-error":
                        input = GetLastError();
                        if (input.Length > 0)
                        {
                            WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Last error: {input}{RESET}");
                            SendPrompt(input, debug, _cancelToken);
                        }
                        break;
                    default:
                        if (input.Length > 0)
                        {
                            SendPrompt(input, debug, _cancelToken);
                        }
                        break;
                }
            }

            Console.TreatControlCAsInput = false;
        }

        private void GetCodeSnippet(string input)
        {
            // split input into lines
            var lines = input.Split(new[] { '\n' });
            var codeSnippet = new StringBuilder();
            // find the first line that starts with ```powershell and copy the lines until ``` is found
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

        private void CopyToClipboard(string input)
        {
            _pwsh.Commands.Clear();
            _pwsh.AddCommand("Set-Clipboard");
            _pwsh.AddParameter("Value", input);
            _pwsh.Invoke();
        }

        private void SendPrompt(string input, bool debug, CancellationToken cancelToken)
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
                    Console.CursorVisible = false;
                    Console.Write($"{RESET}{task.Status}... {SPINNER[i++ % SPINNER.Length]}".PadRight(Console.WindowWidth));
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                        if (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers == ConsoleModifiers.Control)
                        {
                            _cancellationTokenSource.Cancel();
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
                        colorOutput.Append(GetPrettyPowerShellScript(codeSnippet.ToString()));
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

                WriteConsole($"{colorOutput.ToString()}{RESET}");
            }
            catch (Exception e)
            {
                WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}EXCEPTION: {e.Message}\n{e.StackTrace}");
            }
        }

        private void WriteConsole(string text)
        {
            AddToBuffer(text);
            Console.Write(text);
        }

        private void WriteLineConsole(string text)
        {
            AddToBuffer(text + "\n");
            Console.WriteLine(text);
        }

        private void WriteLineBuffer(string text)
        {
            AddToBuffer(text + "\n");
        }

        private void AddToBuffer(string text)
        {
            _buffer.Append(text);
            if (_buffer.Length > _maxBuffer)
            {
                _buffer.Remove(0, _buffer.Length - _maxBuffer);
            }
        }

        private void WriteToolbar()
        {
            // lock the top row
            Console.Write($"{ESC}[2;{Console.WindowHeight}r");
            Console.CursorTop = 0;
            Console.CursorLeft = 0;
            var color = PSStyle.Instance.Background.FromRgb(100, 0, 100) + PSStyle.Instance.Foreground.BrightYellow;
            Console.Write($" {color}[Exit '{_exitKeyInfo.Key}']{RESET} {color}[Get-Error 'Ctrl+E']{RESET} {color}[Copy-Code 'Ctrl+C']{RESET}");
        }

        private string GetLastError()
        {
            var errorVar = GetVariableValue("global:error");
            if (errorVar is ArrayList errorArray && errorArray.Count > 0)
            {
                _pwsh.Commands.Clear();
                _pwsh.AddCommand("Get-Error").AddParameter("InputObject", errorArray[0]);
                _pwsh.AddCommand("Out-String");
                var result = _pwsh.Invoke<string>();
                var sb = new StringBuilder();
                foreach (var item in result)
                {
                    sb.AppendLine(item);
                }

                return sb.ToString();
            }
            else
            {
                WriteConsole($"{PSStyle.Instance.Foreground.BrightMagenta}No error found.{RESET}\n");
            }

            return string.Empty;
        }

        private static ConsoleKeyInfo GetPSReadLineKeyHandler()
        {
            var key = "F3";
            /* // TODO: doesn't currently work as the cmdlet is not found
            _pwsh.Commands.Clear();
            _pwsh.AddCommand("Microsoft.PowerShell.Copilot\\Enable-PSCopilotKeyHandler").AddParameter("ReturnChord", true);
            var result = _pwsh.Invoke<string>();
            if (result.Count > 0)
            {
                key = result[0];
            }
            else
            {
                key = "F3";
            }
            */

            return new ConsoleKeyInfo('\0', (ConsoleKey)Enum.Parse(typeof(ConsoleKey), key), shift: false, alt: false, control: false);
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
                string openai_url;
                switch (_model)
                {
                    case Model.GPT4:
                        openai_url = OPENAI_GPT4_URL;
                        break;
                    default:
                        openai_url = OPENAI_GPT35_TURBO_URL;
                        break;
                }
                var response = _httpClient.PostAsync(openai_url , bodyContent, cancelToken).GetAwaiter().GetResult();
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

        private static string GetPrettyPowerShellScript(string script)
        {
            // parse the script to ast
            var ast = Parser.ParseInput(script, out Token[] tokens, out ParseError[] errors);
            // walk through the tokens and color them depending on type
            var sb = new StringBuilder();
            var colorTokens = new List<(int, string)>(); // (start, color)
            foreach (var token in tokens)
            {
                var color = PSStyle.Instance.Foreground.White;
                switch (token.Kind)
                {
                    case TokenKind.Command:
                        color = PSStyle.Instance.Foreground.BrightYellow;
                        break;
                    case TokenKind.Parameter:
                        color = PSStyle.Instance.Foreground.BrightBlack;
                        break;
                    case TokenKind.Number:
                        color = PSStyle.Instance.Foreground.BrightWhite;
                        break;
                    case TokenKind.Variable:
                    case TokenKind.SplattedVariable:
                        color = PSStyle.Instance.Foreground.BrightGreen;
                        break;
                    case TokenKind.StringExpandable:
                    case TokenKind.StringLiteral:
                    case TokenKind.HereStringExpandable:
                    case TokenKind.HereStringLiteral:
                        color = PSStyle.Instance.Foreground.Cyan;
                        break;
                    case TokenKind.Comment:
                        color = PSStyle.Instance.Foreground.Green;
                        break;
                    default:
                        color = PSStyle.Instance.Foreground.White;
                        break;
                }

                if (token.TokenFlags.HasFlag(TokenFlags.CommandName))
                {
                    color = PSStyle.Instance.Foreground.BrightYellow;
                }
                else if (token.TokenFlags.HasFlag(TokenFlags.Keyword))
                {
                    color = PSStyle.Instance.Foreground.BrightCyan;
                }
                else if (token.TokenFlags.HasFlag(TokenFlags.TypeName))
                {
                    color = PSStyle.Instance.Foreground.BrightBlue;
                }
                else if (token.TokenFlags.HasFlag(TokenFlags.MemberName))
                {
                    color = PSStyle.Instance.Foreground.White;
                }
                // check for all operators
                else if (token.Kind == TokenKind.Generic && token.Text.StartsWith("-"))
                {
                    color = PSStyle.Instance.Foreground.BrightBlack;
                }

                colorTokens.Add((token.Extent.StartOffset, color));
            }

            // walk backwards through the tokens and insert the color codes
            sb.Append(script);
            for (int i = colorTokens.Count - 1; i >= 0; i--)
            {
                var (start, color) = colorTokens[i];
                if (start < sb.Length)
                {
                    sb.Insert(start, color);
                }
            }

            return sb.ToString();
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
                    content = "You are an AI assistant with experise in PowerShell and the command line. You are helpful, creative, clever, and very friendly. Responses including PowerShell code are enclosed in ```powershell blocks."
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
                max_tokens = 4096,
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
