using Azure.AI.OpenAI;
using Spectre.Console;
using Microsoft.PowerShell;
using ShellCopilot.Kernel.Commands;
using System.Text;

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
    private readonly MarkdownRender _mdRender;
    private readonly CommandRunner _cmdRunner;

    private readonly Func<int, bool, string> _rlPrompt;
    private CancellationTokenSource _cancellationSource;
    private bool _hasExited;

    internal Shell(bool interactive, bool useAlternateBuffer = false, string historyFileNamePrefix = null)
    {
        _interactive = interactive;
        _useAlternateBuffer = useAlternateBuffer;

        _pager = new Pager(interactive && useAlternateBuffer);
        _config = Configuration.ReadFromConfigFile();
        _service = new BackendService(_config, historyFileNamePrefix);
        _mdRender = new MarkdownRender();
        _cmdRunner = new CommandRunner();

        _rlPrompt = ReadLinePrompt;
        _cancellationSource = new CancellationTokenSource();

        InitializeShell();
    }

    private CancellationToken CancellationToken => _cancellationSource.Token;
    private bool ChatDisabled { get; set; }

    internal Configuration Configuration => _config;
    internal BackendService BackendService => _service;
    internal MarkdownRender MarkdownRender => _mdRender;
    internal CommandRunner CommandRunner => _cmdRunner;

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

    private void ReadLineInitialization()
    {
        PSConsoleReadLineOptions options = PSConsoleReadLine.GetOptions();
        options.RenderHelper = new ReadLine(_cmdRunner);

        PSConsoleReadLine.SetKeyHandler(
            new[] { "Ctrl+d,Ctrl+c" },
            (key, arg) =>
            {
                PSConsoleReadLine.RevertLine();
                PSConsoleReadLine.Insert("/code copy");
                PSConsoleReadLine.AcceptLine();
            },
            "CopyCode",
            "Copy the code snippet from the last response to clipboard.");
    }

    private void InitializeShell()
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

            // Write out the active model information.
            AnsiConsole.WriteLine("\nShell Copilot (v0.1)");
            AnsiConsole.MarkupLine($"Using the model [green]{_config.ActiveModel}[/]:");
            _config.GetModelInUse().DisplayBackendInfo();

            // Write out help.
            AnsiConsole.MarkupLine($"Type {ConsoleRender.FormatInlineCode("/help")} for instructions.");
            AnsiConsole.WriteLine();

            // Set readline configuration.
            ReadLineInitialization();

            // Write out error or warning if pager cannot be resolved while using alternate buffer.
            _pager.ReportAnyResolutionFailure();
            _cmdRunner.LoadBuiltInCommands(this);
        }
    }

    private void CleanupShell(bool hadError = false)
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
        string useCommand = ConsoleRender.FormatInlineCode($"/use");
        string helpCommand = ConsoleRender.FormatInlineCode("/help");

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
            string text = _mdRender.RenderText(response.Content);
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

    private async Task<string> PrintStreamingChatResponse(StreamingChatCompletions streamingChatCompletions)
    {
        var content = new StringBuilder();
        var streamingRender = new StreamingRender();
        using var response = streamingChatCompletions;

        try
        {
            // Hide the cursor position when rendering the streaming response.
            Console.CursorVisible = false;
            await foreach (StreamingChatChoice choice in response.GetChoicesStreaming())
            {
                await foreach (ChatMessage message in choice.GetMessageStreaming())
                {
                    if (string.IsNullOrEmpty(message.Content))
                    {
                        continue;
                    }

                    content.Append(message.Content);
                    string text = _mdRender.RenderText(content.ToString());
                    streamingRender.Refresh(text);
                }
            }
        }
        finally
        {
            Console.CursorVisible = true;
        }

        Console.WriteLine();
        return content.ToString();
    }

    internal void ExitShell()
    {
        _hasExited = true;
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

    internal async Task<StreamingChatCompletions> StreamingChatWithSnipperAsync(string prompt, bool insertToHistory, bool useStderr)
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
                    statusContext => _service.GetStreamingChatResponseAsync(prompt, insertToHistory, CancellationToken))
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
            CleanupShell();
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
        bool hadError = false;

        while (!_hasExited)
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
                if (input.StartsWith('/'))
                {
                    string commandLine = input[1..].Trim();
                    if (commandLine == string.Empty)
                    {
                        AnsiConsole.MarkupLine(ConsoleRender.FormatError("Command is missing."));
                        continue;
                    }

                    try
                    {
                        _cmdRunner.InvokeCommand(commandLine);
                    }
                    catch (Exception e)
                    {
                        AnsiConsole.MarkupLine(ConsoleRender.FormatError(e.Message));
                    }

                    continue;
                }

                // Chat to the AI endpoint.
                if (ChatDisabled)
                {
                    WriteChatDisabledWarning();
                    continue;
                }

                StreamingChatCompletions response = await
                    StreamingChatWithSnipperAsync(input, insertToHistory: true, useStderr: false).ConfigureAwait(false);

                if (response is not null)
                {
                    await PrintStreamingChatResponse(response);
                }
            }
            catch (ShellCopilotException e)
            {
                AnsiConsole.MarkupLine(ConsoleRender.FormatError(e.Message));
                if (e.HandlerAction is ExceptionHandlerAction.Stop)
                {
                    hadError = true;
                    break;
                }
            }
        }

        CleanupShell(hadError);
    }
}
