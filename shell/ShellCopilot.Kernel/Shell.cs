using Azure.AI.OpenAI;
using ColorCode.VT;
using Spectre.Console;

namespace ShellCopilot.Kernel;

internal class Shell
{
    private const string RESET = "\x1b[0m";
    private const string ALTERNATE_SCREEN_BUFFER = "\x1b[?1049h";
    private const string MAIN_SCREEN_BUFFER = "\x1b[?1049l";

    private readonly Configuration _config;
    private readonly BackendService _service;
    private readonly MarkdownRender _render;
    private readonly CancellationToken _cancellationToken;

    internal Shell(bool isInteractive, CancellationToken cancellationToken, bool useAlternateBuffer = true, string historyFileNamePrefix = null)
    {
        _config = Configuration.ReadFromConfigFile();
        _cancellationToken = cancellationToken;

        if (isInteractive)
        {
            InitInteractively(useAlternateBuffer);
        }

        _service = new BackendService(_config, historyFileNamePrefix, cancellationToken);
        _render = new MarkdownRender();
    }

    internal Configuration Configuration => _config;
    internal BackendService BackendService => _service;
    internal MarkdownRender MarkdownRender => _render;

    internal void InitInteractively(bool useAlternateBuffer)
    {
        if (useAlternateBuffer)
        {
            Console.Write(ALTERNATE_SCREEN_BUFFER);
            Console.Clear();
        }

        AnsiConsole.Write(new FigletText("Shell Copilot").LeftJustified().Color(Color.DarkGoldenrod));
        AnsiConsole.Write(new Rule($"[yellow]AI for the command line[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Using the model [green]{_config.ActiveModel}[/].");
        AnsiConsole.MarkupLine($"Type {ConsoleRender.FormatInlineCode(":help")} for instructions.");
        AnsiConsole.WriteLine();
    }

    internal static void RestoreScreenBuffer(bool hadError)
    {
        if (hadError)
        {
            string message = "Press any key to return to the main screen buffer ...";
            AnsiConsole.Markup(ConsoleRender.FormatErrorMessage(message, usePrefix: false));
            AnsiConsole.Console.Input.ReadKey(intercept: true);
        }

        Console.Write(MAIN_SCREEN_BUFFER);
    }

    private static string GetWarningBasedOnFinishReason(CompletionsFinishReason reason)
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

    private async Task<ChatResponse> ChatWhileRunningSnipper(string prompt, bool insertToHistory, bool useErrStream)
    {
        using var disposable = useErrStream ? ConsoleRender.UseErrorConsole() : null;
        return await AnsiConsole
            .Status()
            .AutoRefresh(true)
            .Spinner(AsciiLetterSpinner.Default)
            .SpinnerStyle(new Style(Color.Cyan2, null, Decoration.Italic))
            .StartAsync(
                "[italic yellow] Generating response[/]",
                statusContext => _service.GetChatResponseAsync(prompt, insertToHistory, _cancellationToken))
            .ConfigureAwait(false);
    }

    internal async Task RunAsync()
    {
        AnsiConsole.Write("ShellCopilot> ");
        await AnsiConsole.Console.Input.ReadKeyAsync(intercept: true, _cancellationToken);
        RestoreScreenBuffer(hadError: false);
    }

    internal async Task RunOnceAsync(string prompt)
    {
        try
        {
            ChatResponse response = await ChatWhileRunningSnipper(prompt, insertToHistory: true, useErrStream: true).ConfigureAwait(false);
            Console.WriteLine(_render.RenderText(response.Content));

            string warning = GetWarningBasedOnFinishReason(response.FinishReason);
            if (warning is not null)
            {
                AnsiConsole.MarkupInterpolated($"[yellow]{warning}[/]");
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine(ConsoleRender.FormatErrorMessage("Operation was aborted."));
        }
        catch (ShellCopilotException exception)
        {
            AnsiConsole.MarkupLine(ConsoleRender.FormatErrorMessage(exception.Message));
        }
    }
}
