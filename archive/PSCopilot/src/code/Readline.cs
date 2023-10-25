using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Text;
using System.Threading;

namespace Microsoft.PowerShell.Copilot
{
    internal class Readline
    {
        private static readonly string PROMPT = $"{PSStyle.Instance.Foreground.BrightGreen}Copilot> {PSStyle.Instance.Foreground.White}";
        private static List<string> _history = new();
        private static int _maxHistory = 256;
        private static OpenAI _openai = new();

        internal static void EnterInputLoop(PSCmdlet cmdlet, CancellationToken cancelToken)
        {
            bool debug = false;
            bool exit = false;
            Console.TreatControlCAsInput = true;
            var promptLength = (new StringDecorated(PROMPT)).ContentLength;
            var inputBuilder = new StringBuilder();
            var consoleHeight = Console.WindowHeight;
            var consoleWidth = Console.WindowWidth;
            while (!exit)
            {
                var historyIndex = _history.Count - 1;
                inputBuilder.Clear();
                Screenbuffer.WriteLineConsole($"{Screenbuffer.RESET}");
                Screenbuffer.WriteConsole(PROMPT);

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
                            Screenbuffer.WriteLineBuffer(inputBuilder.ToString());
                            Screenbuffer.WriteLineConsole("");

                            // see if terminal was resized
                            if (consoleHeight != Console.WindowHeight || consoleWidth != Console.WindowWidth)
                            {
                                consoleHeight = Console.WindowHeight;
                                consoleWidth = Console.WindowWidth;
                                Screenbuffer.RedrawScreen();
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
                            Screenbuffer.WriteLineConsole(inputBuilder.ToString());
                            Screenbuffer.WriteLineConsole("");
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
                            Screenbuffer.WriteLineConsole(inputBuilder.ToString());
                            Screenbuffer.WriteLineConsole("");
                            break;
                        default:
                            if (keyInfo == EnterCopilot._exitKeyInfo)
                            {
                                Screenbuffer.RemoveLastLine();
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
                        Screenbuffer.WriteLineConsole($"\n{highlightOff}Just type whatever you want to send to Copilot.");
                        Screenbuffer.WriteLineConsole($"{highlight}Up{highlightOff} and {highlight}down{highlightOff} arrows will cycle through your history.");
                        Screenbuffer.WriteLineConsole($"{highlight}Ctrl+u{highlightOff} will clear the current line.");
                        Screenbuffer.WriteLineConsole($"{highlight}Ctrl+c{highlightOff} or {highlight}Copy-Code{highlightOff} will copy the current line to the clipboard.");
                        Screenbuffer.WriteLineConsole($"{highlight}Ctrl+e{highlightOff} or {highlight}Get-Error{highlightOff} will get the last error.");
                        Screenbuffer.WriteLineConsole($"Type {highlight}exit{highlightOff} to exit the chat.");
                        Screenbuffer.WriteLineConsole($"Type {highlight}clear{highlightOff} to clear the screen.\n");
                        Screenbuffer.WriteLineConsole($"{highlight}$env:{OpenAI.ENDPOINT_ENV_VAR}{highlightOff} sets the endpoint URL.");
                        Screenbuffer.WriteLineConsole($"{highlight}$env:{OpenAI.SYSTEM_PROMPT_ENV_VAR}{highlightOff} sets the system prompt.");
                        break;
                    case "clear":
                        Console.Clear();
                        Screenbuffer.Clear();
                        Console.CursorTop = Console.WindowHeight - 2;
                        break;
                    case "debug":
                        debug = !debug;
                        Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Debug mode is now {(debug ? "on" : "off")}.");
                        Screenbuffer.WriteLineConsole($"PID: {Environment.ProcessId}");
                        break;
                    case "history":
                        foreach (var item in _history)
                        {
                            Screenbuffer.WriteLineConsole(item);
                        }
                        break;
                    case "exit":
                        exit = true;
                        Screenbuffer.RemoveLastLine();
                        break;
                    case "copy-code":
                        if (_openai.LastCodeSnippet().Length > 0)
                        {
                            Pwsh.CopyToClipboard(_openai.LastCodeSnippet());
                            Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Code snippet copied to clipboard.{Screenbuffer.RESET}");
                        }
                        break;
                    case "get-error":
                        input = Pwsh.GetLastError(cmdlet);
                        if (input.Length > 0)
                        {
                            Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Last error: {input}{Screenbuffer.RESET}");
                            _openai.SendPrompt(input, debug, cancelToken);
                        }
                        break;
                    default:
                        if (input.Length > 0)
                        {
                            _openai.SendPrompt(input, debug, cancelToken);
                        }
                        break;
                }
            }

            Console.TreatControlCAsInput = false;
        }
    }
}
