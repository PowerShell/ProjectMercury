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
        internal static readonly ConsoleKeyInfo _exitKeyInfo = Pwsh.GetPSReadLineKeyHandler();
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
