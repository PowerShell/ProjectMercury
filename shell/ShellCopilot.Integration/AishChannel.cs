﻿using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Integration;

public class AishChannel : IDisposable
{
    private const int MaxNamedPipeNameSize = 104;
    private const int ConnectionTimeout = 7000;

    private readonly string _shellPipeName;
    private readonly Type _psrlType;
    private readonly Runspace _runspace;
    private readonly MethodInfo _psrlInsert, _psrlRevertLine, _psrlAcceptLine;
    private readonly ManualResetEvent _connSetupWaitHandler;
    private readonly AishPredictor _predictor;
    private readonly AishErrorFeedback _feedback;

    private ShellClientPipe _clientPipe;
    private ShellServerPipe _serverPipe;
    private bool? _setupSuccess;
    private Exception _exception;
    private Thread _serverThread;

    private AishChannel(Runspace runspace, Type psConsoleReadLineType)
    {
        _runspace = runspace;
        _psrlType = psConsoleReadLineType;
        _connSetupWaitHandler = new ManualResetEvent(false);

        _shellPipeName = new StringBuilder(MaxNamedPipeNameSize)
            .Append("pwsh_aish.")
            .Append(Environment.ProcessId)
            .Append('.')
            .Append(Path.GetFileNameWithoutExtension(Environment.ProcessPath))
            .ToString();

        BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public;
        _psrlInsert = _psrlType.GetMethod("Insert", bindingFlags, [typeof(string)]);
        _psrlRevertLine = _psrlType.GetMethod("RevertLine", bindingFlags);
        _psrlAcceptLine = _psrlType.GetMethod("AcceptLine", bindingFlags);

        _predictor = new AishPredictor();
        // _feedback = new AishErrorFeedback();
    }

    public static AishChannel CreateSingleton(Runspace runspace, Type psConsoleReadLineType)
    {
        return Singleton ??= new AishChannel(runspace, psConsoleReadLineType);
    }

    internal static AishChannel Singleton { get; private set; }

    internal string PSVersion => _runspace.Version.ToString();

    internal bool Connected => CheckConnection(blocking: true, out _);

    internal bool CheckConnection(bool blocking, out bool setupInProgress)
    {
        setupInProgress = false;
        if (_serverPipe is null)
        {
            // We haven't setup the channel yet.
            return false;
        }

        // We have started the setup, now wait for it to finish.
        int timeout = blocking ? -1 : 0;
        bool signalled = _connSetupWaitHandler.WaitOne(timeout);

        if (signalled)
        {
            return _setupSuccess.Value && _serverPipe.Connected && _clientPipe.Connected;
        }

        setupInProgress = true;
        return false;
    }

    internal string StartChannelSetup()
    {
        if (_serverPipe is not null)
        {
            if (Connected)
            {
                throw new InvalidOperationException("A connected channel already exists.");
            }

            // Channel is not in the connected state, so we can refresh everything and try setting it up again.
            Reset();
        }

        _serverPipe = new ShellServerPipe(_shellPipeName);
        _serverPipe.OnAskConnection += OnAskConnection;
        _serverPipe.OnAskContext += OnAskContext;
        _serverPipe.OnPostCode += OnPostCode;

        _serverThread = new Thread(ThreadProc)
            {
                IsBackground = true,
                Name = "pwsh channel thread"
            };

        _serverThread.Start();
        return _shellPipeName;
    }

    private async void ThreadProc()
    {
        await _serverPipe.StartProcessingAsync(ConnectionTimeout, CancellationToken.None);
    }

    internal void PostQuery(PostQueryMessage message)
    {
        ThrowIfNotConnected();
        _clientPipe.PostQuery(message);
    }

    public void Dispose()
    {
        Reset();
        _connSetupWaitHandler.Dispose();
        _predictor.Unregister();
        //_feedback.Unregister();
        GC.SuppressFinalize(this);
    }

    private void Reset()
    {
        _serverPipe?.Dispose();
        _clientPipe?.Dispose();
        _connSetupWaitHandler.Reset();

        if (_serverPipe is not null)
        {
            _serverPipe.OnAskConnection -= OnAskConnection;
            _serverPipe.OnAskContext -= OnAskContext;
            _serverPipe.OnPostCode -= OnPostCode;
        }

        _serverPipe = null;
        _clientPipe = null;
        _exception = null;
        _serverThread = null;
        _setupSuccess = null;
    }

    private void ThrowIfNotConnected()
    {
        if (!Connected)
        {
            if (_setupSuccess is null)
            {
                throw new NotSupportedException("Channel has not been setup yet.");
            }

            if (_setupSuccess.Value)
            {
                throw new NotSupportedException($"Both the client and server pipes may have been closed. Pipe connection status: client({_clientPipe.Connected}), server({_serverPipe.Connected}).");
            }

            Debug.Assert(_exception is not null, "Error should be set when connection failed.");
            throw new NotSupportedException($"Bi-directional channel could not be established: {_exception.Message}", _exception);
        }
    }

    private void OnPostCode(PostCodeMessage postCodeMessage)
    {
        if (!Console.TreatControlCAsInput || postCodeMessage.CodeBlocks.Count is 0)
        {
            return;
        }

        string codeToInsert;
        List<string> codeForPrediction = null;
        List<string> codeBlocks = postCodeMessage.CodeBlocks;

        if (codeBlocks.Count > 1)
        {
            bool allOneLiners = true;
            foreach (string code in codeBlocks)
            {
                if (code.Contains('\n'))
                {
                    allOneLiners = false;
                    break;
                }
            }

            if (allOneLiners)
            {
                codeToInsert = codeBlocks[0];
                codeForPrediction = codeBlocks;
            }
            else
            {
                // Use LF as line ending to be consistent with the response from LLM.
                StringBuilder sb = new(capacity: 50);
                for (int i = 0; i < codeBlocks.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append('\n');
                    }

                    sb.Append(codeBlocks[i]).Append('\n');
                }

                codeToInsert = sb.ToString();
            }
        }
        else
        {
            codeToInsert = codeBlocks[0];
        }

        if (Console.TreatControlCAsInput)
        {
            PSRLRevertLine();
            PSRLInsert(codeToInsert);
            if (codeForPrediction is not null)
            {
                _predictor.SetCandidates(codeForPrediction);
            }
        }
    }

    private PostContextMessage OnAskContext(AskContextMessage askContextMessage)
    {
        // Not implemented yet.
        return null;
    }

    private void OnAskConnection(ShellClientPipe clientPipe, Exception exception)
    {
        if (clientPipe is not null)
        {
            _clientPipe = clientPipe;
            _setupSuccess = true;
        }
        else
        {
            _setupSuccess = false;
            _exception = exception;
        }

        _connSetupWaitHandler.Set();
    }

    private void PSRLInsert(string text)
    {
        _psrlInsert.Invoke(null, [text]);
    }

    private void PSRLRevertLine()
    {
        _psrlRevertLine.Invoke(null, [null, null]);
    }

    private void PSRLAcceptLine()
    {
        _psrlAcceptLine.Invoke(null, [null, null]);
    }
}

public class Init : IModuleAssemblyCleanup
{
    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        AishChannel.Singleton?.Dispose();
    }
}
