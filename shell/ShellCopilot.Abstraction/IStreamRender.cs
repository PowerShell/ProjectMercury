namespace ShellCopilot.Abstraction;

public interface IStreamRender : IDisposable
{
    string AccumulatedContent { get; }
    void Refresh(string newChunk);
}
