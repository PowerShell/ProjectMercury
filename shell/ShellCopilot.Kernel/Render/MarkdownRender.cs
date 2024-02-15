using Markdig;
using Markdown.VT;

namespace ShellCopilot.Kernel;

internal class CodeBlockVisitor : IVTRenderVisitor
{
    internal CodeBlockVisitor()
    {
        CodeBlock = [];
    }

    internal List<string> CodeBlock { get; }

    internal void Reset()
    {
        CodeBlock.Clear();
    }

    public void VisitCodeBlock(string code)
    {
        CodeBlock.Add(code);
    }
}

internal class MarkdownRender
{
    private readonly VTRenderer _vtRender;
    private readonly MarkdownPipeline _pipeline;
    private readonly StringWriter _stringWriter;
    private readonly CodeBlockVisitor _visitor;

    internal MarkdownRender()
    {
        _stringWriter = new StringWriter();
        _visitor = new CodeBlockVisitor();
        _vtRender = new VTRenderer(_stringWriter, new PSMarkdownOptionInfo(), _visitor);
        _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    }

    internal List<string> GetAllCodeBlocks()
    {
        var code = _visitor.CodeBlock;
        return code.Count > 0 ? code : null;
    }

    internal string GetLastCodeBlock()
    {
        var code = _visitor.CodeBlock;
        return code.Count > 0 ? code[^1] : null;
    }

    internal string RenderText(string text)
    {
        try
        {
            // Reset the visitor before rendering a new markdown text.
            _visitor.Reset();
            return Markdig.Markdown.Convert(text, _vtRender, _pipeline).ToString();
        }
        finally
        {
            // Clear the 'StringWriter' so that the next rendering can start fresh.
            _stringWriter.GetStringBuilder().Clear();
        }
    }
}
