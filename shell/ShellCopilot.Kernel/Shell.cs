using Azure.AI.OpenAI;
using Markdig;
using Markdown.VT;
using Spectre.Console;

namespace ShellCopilot;

internal class MarkdownRender
{
    private readonly VTRenderer _vtRender;
    private readonly MarkdownPipeline _pipeline;
    private readonly StringWriter _stringWriter;

    internal MarkdownRender()
    {
        _stringWriter = new StringWriter();
        _vtRender = new VTRenderer(_stringWriter, new PSMarkdownOptionInfo());
        _vtRender.PushIndent("  ");
        _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    }

    internal string RenderText(string text)
    {
        try
        {
            return Markdig.Markdown.Convert(text, _vtRender, _pipeline).ToString();
        }
        finally
        {
            // Clear the 'StringWriter' so that the next rendering can start fresh.
            _stringWriter.GetStringBuilder().Clear();
        }
    }
}

internal class Shell
{
    private readonly BackendService _service;
    private readonly MarkdownRender _render;

    internal Shell(bool loadChatHistory, string chatHistoryFile = null)
    {
        _service = new BackendService(loadChatHistory, chatHistoryFile);
        _render = new MarkdownRender();
    }

    internal BackendService BackendService => _service;
    internal MarkdownRender MarkdownRender => _render;

    private string GetWarningBasedOnFinishReason(CompletionsFinishReason reason)
    {
        if (reason == CompletionsFinishReason.TokenLimitReached)
        {
            return "Warning: The response may not be complete as the max token limit was exhausted.";
        }

        if (reason == CompletionsFinishReason.ContentFiltered)
        {
            return "Warning: The response is not complete as it was identified as potentially sensitive per content moderation policies.";
        }

        return null;
    }

    internal void Run()
    {
    }

    internal void RunOnce(string prompt)
    {
        ChatResponse response = AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.SimpleDotsScrolling)
            .Start<ChatResponse>(
                "[yellow] Generating response[/]",
                statusContext => _service.GetChatResponse(prompt, insertToHistory: false)
            );

        Console.WriteLine(_render.RenderText(response.Content));

        string warning = GetWarningBasedOnFinishReason(response.FinishReason);
        if (warning is not null)
        {
            AnsiConsole.MarkupInterpolated($"[yellow]{warning}[/]");
        }
    }
}
