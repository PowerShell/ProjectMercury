using System.Reflection;
using Microsoft.PowerShell;

namespace ShellCopilot.Kernel;

/// <summary>
/// When a channel is established between PowerShell and AISH, we need to be able to
/// cancel a 'ReadLine' operation when a request comes in from the PowerShell side,
/// and to do so, we need to override the 'ReadKey' proxy method used by 'PSReadLine'
/// with our implementation.
/// </summary>
internal class CancellableReadKey
{
    private const int LongWaitForKeySleepTime = 200;
    private const int ShortWaitForKeyTimeout = 5000;
    private const int SpinCheckInterval = 20;

    private readonly ManualResetEventSlim _waitHandle;
    private readonly ConsoleKeyInfo _nullKeyInfo;
    private readonly FieldInfo _readKeyField;

    private Func<CancellationToken, bool> _waitForKeyAvailable;
    private CancellationTokenSource _cancellationSource;

    internal CancellableReadKey()
    {
        _waitHandle = new ManualResetEventSlim();
        _nullKeyInfo = new ConsoleKeyInfo(
            keyChar: ' ',
            ConsoleKey.DownArrow,
            shift: false,
            alt: false,
            control: false);

        _waitForKeyAvailable = LongWaitForKey;
        _readKeyField = typeof(PSConsoleReadLine).Assembly
            .GetType("Microsoft.PowerShell.Internal.VirtualTerminal")
            .GetField("_readKeyOverride", BindingFlags.Static | BindingFlags.NonPublic);
        _readKeyField.SetValue(null, (Func<bool, ConsoleKeyInfo>)ReadKey);
    }

    internal CancellationTokenSource CancellationSource => _cancellationSource;

    internal void RefreshCancellationSourceIfNeeded()
    {
        if (_cancellationSource is null || _cancellationSource.IsCancellationRequested)
        {
            _cancellationSource?.Dispose();
            _cancellationSource = new CancellationTokenSource();
        }
    }

    private ConsoleKeyInfo ReadKey(bool intercept)
    {
        try
        {
            CancellationToken token = _cancellationSource.Token;
            // The '_waitForKeyAvailable' delegate switches between a long delay between check and
            // a short timeout depending on how recently a key has been pressed. This allows us to
            // let the CPU enter low power mode without compromising responsiveness.
            while (!_waitForKeyAvailable(token)) { }
            return token.IsCancellationRequested ? _nullKeyInfo : Console.ReadKey(intercept);
        }
        catch (OperationCanceledException)
        {
            return _nullKeyInfo;
        }
    }

    private bool LongWaitForKey(CancellationToken cancellationToken)
    {
        // Wait for a key to be buffered with a long delay between checks.
        while (!Console.KeyAvailable)
        {
            _waitHandle.Wait(LongWaitForKeySleepTime, cancellationToken);
        }

        // As soon as a key is buffered, return true and switch the wait logic
        // to be more responsive, but also more expensive.
        _waitForKeyAvailable = ShortWaitForKey;
        return true;
    }

    private bool ShortWaitForKey(CancellationToken cancellationToken)
    {
        if (Console.KeyAvailable)
        {
            return true;
        }

        // Set up the timeout for the whole short wait period.
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(ShortWaitForKeyTimeout);

        try
        {
            while (!Console.KeyAvailable)
            {
                // Check frequently for a new key to be buffered.
                _waitHandle.Wait(SpinCheckInterval, source.Token);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            // It was because of timeout.
            // If the user has not pressed a key before the end of the 'ShortWaitForKeyTimeout' then
            // the user is idle and we can switch back to long delays between 'KeyAvailable' checks.
            _waitForKeyAvailable = LongWaitForKey;
            return false;
        }
        finally
        {
            source.Dispose();
        }
    }
}
