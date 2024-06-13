namespace AIShell.Abstraction;

public record CodeBlock(string Code, string Language);

public interface IStreamRender : IDisposable
{
    string AccumulatedContent { get; }
    List<CodeBlock> CodeBlocks { get; }
    void Refresh(string newChunk);
}
