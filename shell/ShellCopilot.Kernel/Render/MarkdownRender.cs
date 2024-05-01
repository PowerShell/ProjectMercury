using Markdig;
using Markdown.VT;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel;

internal class CodeBlockVisitor : IVTRenderVisitor
{
    internal CodeBlockVisitor()
    {
        CodeBlocks = [];
    }

    internal List<CodeBlock> CodeBlocks { get; }

    internal void Reset()
    {
        CodeBlocks.Clear();
    }

    public void VisitCodeBlock(string code, string language)
    {
        CodeBlocks.Add(new CodeBlock(code.Replace("\r\n", "\n"), language));
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

    internal List<CodeBlock> GetAllCodeBlocks()
    {
        var code = _visitor.CodeBlocks;
        return code.Count > 0 ? code : null;
    }

    internal CodeBlock GetLastCodeBlock()
    {
        var code = _visitor.CodeBlocks;
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
