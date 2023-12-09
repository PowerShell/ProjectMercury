namespace ShellCopilot.Abstraction;

public interface IShellContext
{
    CancellationToken CancellationToken { get; }

    void RenderFullResponse(string response);

    IStreamRender GetStreamRender();

    Task<T> RunWithSpinnerAsync<T>(Func<Task<T>> func, string status = null);
}
