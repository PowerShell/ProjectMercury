namespace ShellCopilot.Abstraction;

public interface IStreamRender
{
    string AccumulatedContent { get; }
    void Refresh(string newChunk);
}
