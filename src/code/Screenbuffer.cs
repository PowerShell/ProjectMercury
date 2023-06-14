using System;
using System.Management.Automation;
using System.Text;

namespace Microsoft.PowerShell.Copilot
{
    internal class Screenbuffer
    {
        private const string ALTERNATE_SCREEN_BUFFER = "\x1b[?1049h";
        private const string MAIN_SCREEN_BUFFER = "\x1b[?1049l";
        private static readonly string INSTRUCTIONS = $"{PSStyle.Instance.Foreground.Cyan}Type 'help' for instructions.";
        private const char ESC = '\x1b';
        internal static readonly string RESET = $"{PSStyle.Instance.Reset}{PSStyle.Instance.Background.FromRgb(20, 0, 20)}";
        private static StringBuilder _buffer = new();
        private static int _maxBuffer = 4096;
        private const string LOGO = @"

██████╗ ███████╗ ██████╗ ██████╗ ██████╗ ██╗██╗      ██████╗ ████████╗
██╔══██╗██╔════╝██╔════╝██╔═══██╗██╔══██╗██║██║     ██╔═══██╗╚══██╔══╝
██████╔╝███████╗██║     ██║   ██║██████╔╝██║██║     ██║   ██║   ██║
██╔═══╝ ╚════██║██║     ██║   ██║██╔═══╝ ██║██║     ██║   ██║   ██║
██║     ███████║╚██████╗╚██████╔╝██║     ██║███████╗╚██████╔╝   ██║
╚═╝     ╚══════╝ ╚═════╝ ╚═════╝ ╚═╝     ╚═╝╚══════╝ ╚═════╝    ╚═╝   v0.1
";

        internal static void SwitchToAlternateScreenBuffer()
        {
            Console.Write(ALTERNATE_SCREEN_BUFFER);
        }

        internal static void SwitchToMainScreenBuffer()
        {
            Console.Write(MAIN_SCREEN_BUFFER);
        }

        internal static void RedrawScreen()
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
                string openai_url = "GPT-4";
                if (openai_url is null)
                {
                    WriteLineConsole($"{PSStyle.Instance.Foreground.Yellow}Using {EnterCopilot._model}");
                }
                else
                {
                    WriteLineConsole($"{PSStyle.Instance.Foreground.Yellow}Using {openai_url}");
                }

                WriteLineConsole($"{INSTRUCTIONS}");
            }
        }

        internal static void WriteLineBuffer(string text)
        {
            AddToBuffer(text + "\n");
        }

        internal static void AddToBuffer(string text)
        {
            _buffer.Append(text);
            if (_buffer.Length > _maxBuffer)
            {
                _buffer.Remove(0, _buffer.Length - _maxBuffer);
            }
        }

        internal static void WriteConsole(string text)
        {
            Screenbuffer.AddToBuffer(text);
            Console.Write(text);
        }

        internal static void WriteLineConsole(string text)
        {
            Screenbuffer.AddToBuffer(text + "\n");
            Console.WriteLine(text);
        }

        internal static void WriteToolbar()
        {
            // lock the top row
            Console.Write($"{ESC}[2;{Console.WindowHeight}r");
            Console.CursorTop = 0;
            Console.CursorLeft = 0;
            var color = PSStyle.Instance.Background.FromRgb(100, 0, 100) + PSStyle.Instance.Foreground.BrightYellow;
            Console.Write($" {color}[Exit '{EnterCopilot._exitKeyInfo.Key}']{Screenbuffer.RESET} {color}[Get-Error 'Ctrl+E']{Screenbuffer.RESET} {color}[Copy-Code 'Ctrl+C']{Screenbuffer.RESET}");
        }

        internal static void Remove(int index, int count)
        {
            _buffer.Remove(index, count);
        }

        internal static void RemoveLastLine()
        {
            var stringBuffer = _buffer.ToString();
            var last = stringBuffer.LastIndexOf('\n');
            if (last > 0)
            {
                _buffer.Remove(last, _buffer.Length - last);
            }
        }

        internal static void Clear()
        {
            _buffer.Clear();
        }
    }
}
