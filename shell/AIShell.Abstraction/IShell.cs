namespace AIShell.Abstraction;

/// <summary>
/// The shell interface to interact with the AIShell.
/// </summary>
public interface IShell
{
    /// <summary>
    /// The host of the AIShell.
    /// </summary>
    IHost Host { get; }

    /// <summary>
    /// The token to indicate cancellation when `Ctrl+c` is pressed by user.
    /// </summary>
    CancellationToken CancellationToken { get; }

    // TODO:
    // - methods to run code: python, command-line, powershell, node-js.
    // - methods to communicate with shell client.
}
