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

    /// <summary>
    /// Extracts code blocks that are surrounded by code fences from the passed-in markdown text.
    /// </summary>
    /// <param name="text">The markdown text.</param>
    /// <returns>A list of code blocks or null if there is no code block.</returns>
    List<CodeBlock> ExtractCodeBlocks(string text, out List<SourceInfo> sourceInfos);

    // TODO:
    // - methods to run code: python, command-line, powershell, node-js.
    // - methods to communicate with shell client.
}
