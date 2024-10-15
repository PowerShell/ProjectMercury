namespace AIShell.Abstraction;

/// <summary>
/// Represents a code block from a markdown text.
/// </summary>
public record CodeBlock(string Code, string Language);

/// <summary>
/// Represents the source metadata information of a code block extracted from a given markdown text.
/// </summary>
/// <param name="Start">The start index of the code block within the text.</param>
/// <param name="End">The end index of the code block within the text.</param>
/// <param name="Indents">Number of spaces for indentation used by the code block.</param>
public record SourceInfo(int Start, int End, int Indents);

public interface IStreamRender : IDisposable
{
    string AccumulatedContent { get; }
    List<CodeBlock> CodeBlocks { get; }
    void Refresh(string newChunk);
}
