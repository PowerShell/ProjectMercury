// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.Copilot
{
    public sealed partial class EnterCopilot : PSCmdlet
    {
        private static CancellationTokenSource _cancellationTokenSource = new();
        private static CancellationToken _cancelToken = _cancellationTokenSource.Token;
        internal static readonly ConsoleKeyInfo _exitKeyInfo = Pwsh.GetShellReadLineKeyHandler();
        //internal static Model? _model = Program.getCurrentModel();
        private static OpenAI? _openai;

        [Parameter(Mandatory = false)]
        public SwitchParameter LastError { get; set; }

        public EnterCopilot(bool restore)
        {
            try
            {
                _openai = new OpenAI();
                Process(restore);
            }
            catch (Exception e)
            {
                ThrowTerminatingError(new ErrorRecord(e, "OpenAIError", ErrorCategory.InvalidArgument, null));
            }
        }

        protected override void BeginProcessing()
        {
            base.BeginProcessing();
            Process(true);
        }

        internal static void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        public void Process(bool restore)
        {
            try
            {
                Screenbuffer.SwitchToAlternateScreenBuffer();
                Screenbuffer.RedrawScreen();

                if(restore == false)
                {
                    Screenbuffer.WriteLineConsole($"\n{PSStyle.Instance.Foreground.Yellow}Welcome to the chat session of ShellCopilot!\n");
                    Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.Cyan}You can ask questions and get responses back from the current active registered model. In order to run the code you are suggested you will need to exit the chat experience with the exit command, and run the code in your normal shell experience. You can switch back and forth between this chat and your shell by using the F3 key or using ai --restore. Use the help command in the chat experience and the ai --help command in your shell to show the available commands.\n");
                }
                
                if (LastError)
                {
                    var input = Pwsh.GetLastError(this);
                    if (input.Length > 0)
                    {
                        Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightMagenta}Last error: {input}{Screenbuffer.RESET}");
                        _openai?.SendPrompt(input, restore, false, _cancelToken);
                    }
                }
                Program.printHistory(restore);
                Readline.EnterInputLoop(this, restore, _cancelToken);
            }
            finally
            {
                Screenbuffer.SwitchToMainScreenBuffer();
            }
        }
    }
}
