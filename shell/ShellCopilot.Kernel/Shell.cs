using Azure.AI.OpenAI;
using Spectre.Console;
using Microsoft.PowerShell;

namespace ShellCopilot.Kernel;

internal class Shell
{
    private const string ALTERNATE_SCREEN_BUFFER = "\x1b[?1049h";
    private const string MAIN_SCREEN_BUFFER = "\x1b[?1049l";

    private readonly bool _interactive;
    private readonly bool _useAlternateBuffer;
    private readonly Pager _pager;
    private readonly Configuration _config;
    private readonly BackendService _service;
    private readonly MarkdownRender _render;
    private readonly Func<int, bool, string> _rlPrompt;
    private CancellationTokenSource _cancellationSource;

    internal Shell(bool interactive, bool useAlternateBuffer = false, string historyFileNamePrefix = null)
    {
        _interactive = interactive;
        _useAlternateBuffer = useAlternateBuffer;
        _pager = new Pager(interactive && useAlternateBuffer);
        _config = Configuration.ReadFromConfigFile();
        _service = new BackendService(_config, historyFileNamePrefix);
        _render = new MarkdownRender();
        _rlPrompt = ReadLinePrompt;
        _cancellationSource = new CancellationTokenSource();

        InitShell();
    }

    private CancellationToken CancellationToken => _cancellationSource.Token;
    private bool ChatDisabled { get; set; }

    internal Configuration Configuration => _config;
    internal BackendService BackendService => _service;
    internal MarkdownRender MarkdownRender => _render;

    /// <summary>
    /// For reference:
    /// https://github.com/dotnet/command-line-api/blob/67df30a1ac4152e7f6278847b88b8f1ea1492ba7/src/System.CommandLine/Invocation/ProcessTerminationHandler.cs#L73
    /// TODO: We may want to implement `OnPosixSignal` too for more reliable cancellation on non-Windows.
    /// </summary>
    private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
    {
        // Set the Cancel property to true to prevent the process from terminating.
        args.Cancel = true;
        switch (args.SpecialKey)
        {
            // Treat both Ctrl-C and Ctrl-Break as the same.
            case ConsoleSpecialKey.ControlC:
            case ConsoleSpecialKey.ControlBreak:
                // Request cancellation and refresh the cancellation source.
                _cancellationSource.Cancel();
                _cancellationSource = new CancellationTokenSource();
                return;
        }
    }

    private void InitShell()
    {
        ChatDisabled = false;
        Console.CancelKeyPress += OnCancelKeyPress;

        if (_interactive)
        {
            if (_useAlternateBuffer)
            {
                Console.Write(ALTERNATE_SCREEN_BUFFER);
                Console.Clear();
            }

            // Write out the ASCII art text.
            AnsiConsole.Write(new FigletText("Shell Copilot").LeftJustified().Color(Color.DarkGoldenrod));
            AnsiConsole.Write(new Rule($"[yellow]AI for the command line[/]").RuleStyle("grey").LeftJustified());

            // Write out the active model information.
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Using the model [green]{_config.ActiveModel}[/]:");
            _config.GetModelInUse().DisplayBackendInfo();

            // Write out help.
            AnsiConsole.MarkupLine($"Type {ConsoleRender.FormatInlineCode(":help")} for instructions.");
            AnsiConsole.WriteLine();

            // Write out error or warning if pager cannot be resolved while using alternate buffer.
            _pager.ReportIfPagerCannotBeResolved();
        }
    }

    internal void ExitShell(bool hadError = false)
    {
        if (_interactive && _useAlternateBuffer)
        {
            if (hadError)
            {
                string message = "Press any key to return to the main screen buffer ...";
                AnsiConsole.Markup(ConsoleRender.FormatError(message, usePrefix: false));
                AnsiConsole.Console.Input.ReadKey(intercept: true);
            }

            Console.Write(MAIN_SCREEN_BUFFER);
        }

        Console.CancelKeyPress -= OnCancelKeyPress;
    }

    private static string ReadLinePrompt(int count, bool chatDisabled)
    {
        var indicator = chatDisabled ? ConsoleRender.FormatWarning(" ! ", usePrefix: false) : null;
        return $"[bold green]aish[/]:{count}{indicator}> ";
    }

    private static string GetWarningBasedOnFinishReason(CompletionsFinishReason reason)
    {
        if (reason.Equals(CompletionsFinishReason.TokenLimitReached))
        {
            return "The response may not be complete as the max token limit was exhausted.";
        }

        if (reason.Equals(CompletionsFinishReason.ContentFiltered))
        {
            return "The response is not complete as it was identified as potentially sensitive per content moderation policies.";
        }

        return null;
    }

    private static void WriteChatDisabledWarning()
    {
        string useCommand = ConsoleRender.FormatInlineCode($":{Utils.AppName} use");
        string helpCommand = ConsoleRender.FormatInlineCode(":help");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(ConsoleRender.FormatWarning("Chat disabled due to the missing access key."));
        AnsiConsole.MarkupLine(ConsoleRender.FormatWarning($"Run {useCommand} to switch to a different model. Type {helpCommand} for more instructions."));
        AnsiConsole.WriteLine();
    }

    private void PrintChatResponse(ChatResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Content))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(ConsoleRender.FormatNote("Received response is empty or contains whitespace only."));
        }
        else if (Console.IsOutputRedirected)
        {
            Console.WriteLine(response.Content);
        }
        else
        {
            // Render the markdown only if standard output is not redirected.
            string text = _render.RenderText(response.Content);
            if (!Utils.LeadingWhiteSpaceHasNewLine(text))
            {
                Console.WriteLine();
            }

            _pager.WriteOutput(text);
        }

        string warning = GetWarningBasedOnFinishReason(response.FinishReason);
        if (warning is not null)
        {
            AnsiConsole.MarkupLine(ConsoleRender.FormatWarning(warning));
            AnsiConsole.WriteLine();
        }
    }

    internal bool EnsureKeyPresentForActiveModel()
    {
        AiModel model = _config.GetModelInUse();
        if (model.Key is null && model.RequestForKey(mandatory: true, CancellationToken, showBackendInfo: false))
        {
            Configuration.WriteToConfigFile(_config);
        }

        return model.Key is not null;
    }

    internal async Task<ChatResponse> ChatWithSnipperAsync(string prompt, bool insertToHistory, bool useStderr)
    {
        using var _ = useStderr ? ConsoleRender.UseErrorConsole() : null;
        Capabilities caps = AnsiConsole.Profile.Capabilities;
        bool interactive = caps.Interactive;

        try
        {
            // When standard input is redirected, AnsiConsole's auto detection believes it's non-interactive,
            // and thus doesn't render Status or Progress. However, redirected input should not affect the
            // Status/Progress rendering as long as its output target, stderr or stdout, is not redirected.
            caps.Interactive = true;

            return await AnsiConsole
                .Status()
                .AutoRefresh(true)
                .Spinner(AsciiLetterSpinner.Default)
                .SpinnerStyle(new Style(Color.Olive))
                .StartAsync(
                    "[italic slowblink]Generating...[/]",
                    statusContext => _service.GetChatResponseAsync(prompt, insertToHistory, CancellationToken))
                .ConfigureAwait(false);
        }
        finally
        {
            caps.Interactive = interactive;
        }
    }

    internal async Task RunOnceAsync(string prompt)
    {
        AiModel model = _config.GetModelInUse();
        if (model.Key is null)
        {
            string setCommand = ConsoleRender.FormatInlineCode($"{Utils.AppName} set");
            string helpCommand = ConsoleRender.FormatInlineCode($"{Utils.AppName} set -h");

            using var _ = ConsoleRender.UseErrorConsole();
            AnsiConsole.MarkupLine(ConsoleRender.FormatError($"Access key is missing for the active model '{model.Name}'."));
            AnsiConsole.MarkupLine(ConsoleRender.FormatError($"You can set the key by {setCommand}. Run {helpCommand} for details."));

            return;
        }

        try
        {
            ChatResponse response = Console.IsOutputRedirected && Console.IsErrorRedirected
                ? await _service.GetChatResponseAsync(prompt, insertToHistory: false, CancellationToken).ConfigureAwait(false)
                : await ChatWithSnipperAsync(prompt, insertToHistory: false, Console.IsOutputRedirected).ConfigureAwait(false);

            if (response is not null)
            {
                PrintChatResponse(response);
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine(ConsoleRender.FormatError("Operation was aborted."));
        }
        catch (ShellCopilotException exception)
        {
            AnsiConsole.MarkupLine(ConsoleRender.FormatError(exception.Message));
        }
        finally
        {
            ExitShell();
        }
    }

    internal async Task RunREPLAsync()
    {
        if (!EnsureKeyPresentForActiveModel())
        {
            ChatDisabled = true;
            WriteChatDisabledWarning();
        }

        int count = 1;
        bool hadError;

        while (true)
        {
            hadError = false;
            string rlPrompt = _rlPrompt(count, ChatDisabled);
            AnsiConsole.Markup(rlPrompt);

            try
            {
                string input = PSConsoleReadLine.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                count++;
                if (input.StartsWith(':'))
                {
                    string command = input[1..].Trim();
                    if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    // TODO: Handle commands
                    AnsiConsole.MarkupLineInterpolated($"[cyan]Received '{input}'. Command handling is coming soon.[/]");
                    continue;
                }

                // Chat to the AI endpoint.
                if (ChatDisabled)
                {
                    WriteChatDisabledWarning();
                    continue;
                }

                ChatResponse response = await
                    ChatWithSnipperAsync(
                        input,
                        insertToHistory: true,
                        useStderr: false)
                    .ConfigureAwait(false);

                if (response is not null)
                {
                    PrintChatResponse(response);
                }
            }
            catch (ShellCopilotException e)
            {
                AnsiConsole.MarkupLine(ConsoleRender.FormatError(e.Message));
                if (e.HandlerAction is ExceptionHandlerAction.Stop)
                {
                    break;
                }
            }
        }

        ExitShell(hadError);
    }
}
