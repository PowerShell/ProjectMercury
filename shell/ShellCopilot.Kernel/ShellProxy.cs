
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel;

internal class ShellProxy : IShellContext
{
    private readonly Shell _shell;
    private readonly Host _host;

    internal ShellProxy(Shell shell)
    {
        _shell = shell;
        _host = shell.Host;
    }

    CancellationToken IShellContext.CancellationToken => _shell.CancellationToken;

    void IShellContext.RenderFullResponse(string response) => _host.RenderFullResponse(response);

    IStreamRender IShellContext.CreateStreamRender() => _host.NewStreamRender(_shell.CancellationToken);

    Task<T> IShellContext.RunWithSpinnerAsync<T>(Func<Task<T>> func, string status) => _host.RunWithSpinnerAsync(func, status);
}
