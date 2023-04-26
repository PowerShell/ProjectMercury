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
    public enum Model
    {
        GPT35_Turbo,
        GPT4,
        GPT4_32K,
    }

    [Alias("Copilot")]
    [Cmdlet(VerbsCommon.Enter, "Copilot")]
    public sealed partial class EnterCopilot : PSCmdlet
    {
        private const string MODEL = "gpt-35-turbo";
        private const int MAX_TOKENS = 64;
        private static CancellationTokenSource _cancellationTokenSource = new();
        private static CancellationToken _cancelToken = _cancellationTokenSource.Token;
        internal static readonly ConsoleKeyInfo _exitKeyInfo = Pwsh.GetPSReadLineKeyHandler();
        internal static Model _model = Model.GPT35_Turbo;
        private static OpenAI _openai;

        [Parameter(Mandatory = false)]
        public SwitchParameter LastError { get; set; }

        [Parameter(Mandatory = false)]
        public Model Model
        {
            get { return _model; }
            set { _model = value; }
        }

        public EnterCopilot()
        {
            try
            {
                _openai = new OpenAI();
            }
            catch (Exception e)
            {
                ThrowTerminatingError(new ErrorRecord(e, "OpenAIError", ErrorCategory.InvalidArgument, null));
            }
        }

        protected override void BeginProcessing()
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
                        _openai.SendPrompt(input, false, _cancelToken);
                    }
                }

                Readline.EnterInputLoop(this, _cancelToken);
            }
            finally
            {
                Screenbuffer.SwitchToMainScreenBuffer();
            }
        }

        internal static void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
