namespace ShellCopilot.Abstraction;

public interface IShellContext
{
    /// <summary>
    /// The token that indicates cancellation when `Ctrl+c` is pressed by user.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Render a full response to console output.
    ///  - if stdout is redirected, the raw response will be written out to stdout;
    ///  - otherwise, response will be parsed as markdown and rendered with rich UI elements to stdout.
    /// </summary>
    void RenderFullResponse(string response);

    /// <summary>
    /// Creates a render to handle the streaming response.
    /// The returned render parses the response as markdown and writes rich UI elements to stdout, which
    /// refresh dynamically as new response chunks coming in.
    /// </summary>
    IStreamRender CreateStreamRender();

    /// <summary>
    /// Run an asynchronouse task with a spinner on the console showing the task is in progress.
    /// </summary>
    /// <typeparam name="T">The return type of the asynchronouse task.</typeparam>
    /// <param name="func">The asynchronouse task.</param>
    /// <returns>The returned result of <paramref name="func"/></returns>
    Task<T> RunWithSpinnerAsync<T>(Func<Task<T>> func) => RunWithSpinnerAsync(func, "Generating...");
    Task<T> RunWithSpinnerAsync<T>(Func<Task<T>> func, string status = null);
}
