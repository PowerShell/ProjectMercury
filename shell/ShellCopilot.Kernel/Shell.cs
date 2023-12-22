using System.Reflection;
using Azure.AI.OpenAI;
using Microsoft.PowerShell;
using ShellCopilot.Abstraction;
using ShellCopilot.Kernel.Commands;
using Spectre.Console;

namespace ShellCopilot.Kernel;

internal class Shell
{
    private readonly bool _isInteractive;
    private readonly List<LLMAgent> _agents;
    private readonly Stack<LLMAgent> _activeAgentStack;

    private CancellationTokenSource _cancellationSource;
    private LLMAgent ActiveAgent => _activeAgentStack.TryPeek(out var agent) ? agent : null;

    internal bool Exit { set; get; }
    internal Host Host { get; }
    internal CommandRunner CommandRunner { get; }
    internal CancellationToken CancellationToken => _cancellationSource.Token;

    internal Shell(bool interactive, bool useAlternateBuffer = false, string historyFileNamePrefix = null)
    {
        _isInteractive = interactive;
        _agents = new List<LLMAgent>();
        _activeAgentStack = new Stack<LLMAgent>();
        _cancellationSource = new CancellationTokenSource();

        Exit = false;
        Host = new Host();

        if (interactive)
        {
            Host.WriteLine("Shell Copilot (v0.1)");
        }

        LoadAvailableAgents();
        Console.CancelKeyPress += OnCancelKeyPress;

        if (interactive)
        {
            CommandRunner = new CommandRunner(this);
            SetReadLineExperience();

            // Write out information about the active agent.
            Host.WriteMarkupLine(ActiveAgent is null
                ? string.Empty
                : $"\nUsing the agent [green]{ActiveAgent.Impl.Name}[/]:");

            // Write out help.
            Host.WriteMarkupLine($"Type {ConsoleRender.FormatInlineCode("/help")} for instructions.")
                .WriteLine();
        }
    }

    /// <summary>
    /// Load a plugin assembly file and process the agents defined in it.
    /// </summary>
    internal void ProcessAgentPlugin(string pluginFile)
    {
        Assembly plugin = Assembly.LoadFrom(pluginFile);
        foreach (Type type in plugin.ExportedTypes)
        {
            if (!typeof(ILLMAgent).IsAssignableFrom(type))
            {
                continue;
            }

            var agent = (ILLMAgent)Activator.CreateInstance(type);
            var agentHome = Path.Join(Utils.AgentConfigHome, agent.Name);
            var config = new AgentConfig
            {
                IsInteractive = _isInteractive,
                ConfigurationRoot = Directory.CreateDirectory(agentHome).FullName,
                RenderingStyle = Console.IsOutputRedirected
                    ? RenderingStyle.FullResponsePreferred
                    : RenderingStyle.StreamingResponsePreferred
            };

            agent.Initialize(config);
            _agents.Add(new LLMAgent(agent));
        }
    }

    /// <summary>
    /// Load all available agents.
    /// </summary>
    private void LoadAvailableAgents()
    {
        foreach (string dir in Directory.EnumerateDirectories(Utils.AgentHome))
        {
            string name = Path.GetFileName(dir);
            string file = Path.Join(dir, $"{name}.dll");

            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                ProcessAgentPlugin(file);
            }
            catch (Exception ex)
            {
                Host.WriteErrorLine()
                    .WriteErrorMarkupLine($"Failed to load the agent '{name}': {ex.Message}");
            }
        }

        if (_agents.Count is 0)
        {
            Host.WriteLine()
                .WriteMarkupLine(ConsoleRender.FormatWarning($"No agent available."));
            return;
        }

        try
        {
            LLMAgent chosenAgent = Host
                .PromptForSelectionAsync(
                    title: "Select the agent [green]to use[/]:",
                    choices: _agents,
                    cancellationToken: default,
                    converter: static a => a.Impl.Name)
                .GetAwaiter().GetResult();

            _activeAgentStack.Push(chosenAgent);
        }
        catch (Exception)
        {
            // Ignore failure from showing the confirmation prompt.
        }
    }

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

    /// <summary>
    /// Configure the read-line experience.
    /// </summary>
    private void SetReadLineExperience()
    {
        PSConsoleReadLineOptions options = PSConsoleReadLine.GetOptions();
        options.RenderHelper = new ReadLineHelper(CommandRunner);

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

    private void RunCommand(string input)
    {
        string commandLine = input[1..].Trim();
        if (commandLine == string.Empty)
        {
            Host.WriteMarkupLine(ConsoleRender.FormatError("Command is missing."));
            return;
        }

        try
        {
            CommandRunner.InvokeCommand(commandLine);
        }
        catch (Exception e)
        {
            Host.WriteMarkupLine(ConsoleRender.FormatError(e.Message));
        }
    }

    internal async Task RunREPLAsync()
    {
        int count = 1;
        while (!Exit)
        {
            LLMAgent agent = ActiveAgent;
            string indicator = agent is null ? ConsoleRender.FormatWarning(" ! ", usePrefix: false) : null;
            string prompt = $"[bold green]aish[/]:{count}{indicator}> ";
            Host.WriteMarkupLine(prompt);

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
                    RunCommand(input);
                    continue;
                }

                // Now it's a query to the LLM.
                if (agent is null)
                {
                    // No agent to serve the query. Print the warning and go back to read-line prompt.
                    string agentCommand = ConsoleRender.FormatInlineCode($"/agent use");
                    string helpCommand = ConsoleRender.FormatInlineCode("/help");

                    Host.WriteLine()
                        .WriteMarkupLine(ConsoleRender.FormatWarning("No active agent selected, chat is disabled."))
                        .WriteMarkupLine(ConsoleRender.FormatWarning($"Run {agentCommand} to select an agent. Type {helpCommand} for more instructions."))
                        .WriteLine();
                    continue;
                }

                if (agent.IsOrchestrator(out IOrchestrator orchestrator)
                    && _activeAgentStack.Count is 1
                    && !agent.OrchestratorRoleDisabled
                    && _agents.Count > 1)
                {
                    Host.WriteMarkupLine($"The active agent [green]{agent.Impl.Name}[/] can act as an orchestrator and there are multiple agents available.");
                    bool confirmed = await Host.PromptForConfirmationAsync(
                        prompt: $"Do you want it to find the most suitable agent for your query?",
                        cancellationToken: default,
                        defaultValue: false);

                    if (confirmed)
                    {
                        List<string> descriptions = new(capacity: _agents.Count);
                        foreach (LLMAgent item in _agents)
                        {
                            descriptions.Add(item.Impl.Description);
                        }

                        try
                        {
                            Task<int> find_agent_op() => orchestrator.FindAgentForPrompt(
                                prompt: input,
                                agents: descriptions,
                                token: CancellationToken).WaitAsync(CancellationToken);

                            int selected = await Host.RunWithSpinnerAsync(find_agent_op, status: "Thinking...");
                            string agentCommand = ConsoleRender.FormatInlineCode($"/agent pop");

                            if (selected >= 0)
                            {
                                var selectedAgent = _agents[selected];
                                _activeAgentStack.Push(selectedAgent);
                                Host.WriteMarkupLine(ConsoleRender.FormatNote($"Selected agent: [green]{selectedAgent.Impl.Name}[/]"))
                                    .WriteMarkupLine(ConsoleRender.FormatNote($"It's now active for your query. When you are done with the topic, run {agentCommand} to return to the orchestrator."));
                            }
                            else
                            {
                                _activeAgentStack.Push(agent);
                                Host.WriteMarkupLine(ConsoleRender.FormatNote($"No suitable agent was found. The active agent [green]{agent.Impl.Name}[/] will be used for the topic."))
                                    .WriteMarkupLine(ConsoleRender.FormatNote($"When you are done with the topic, run {agentCommand} to return to the orchestrator."));
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex is OperationCanceledException)
                            {
                                // User cancelled the operation, so we return to the read-line prompt.
                                continue;
                            }

                            agent.OrchestratorRoleDisabled = true;
                            Host.WriteLine()
                                .WriteMarkupLine(ConsoleRender.FormatError($"Operation failed: {ex.Message}"))
                                .WriteLine()
                                .WriteMarkupLine(ConsoleRender.FormatNote($"The orchestrator role is disabled due to the failure. Continue with the active agent [green]{agent.Impl.Name}[/] for the query."));
                        }
                    }
                }

                // Use the current active agent for the query.
                agent = ActiveAgent;
                ShellProxy shellProxy = new(this);

                try
                {
                    if (!agent.SelfCheckSucceeded)
                    {
                        // Let the agent self check before using it. It's a chance for the agent to validate its
                        // mandatory settings and maybe even configure the settings interactively with the user.
                        agent.SelfCheckSucceeded = await agent.Impl.SelfCheck(shellProxy);
                    }

                    if (agent.SelfCheckSucceeded)
                    {
                        await agent.Impl.Chat(input, shellProxy).WaitAsync(CancellationToken);
                    }
                    else
                    {
                        Host.WriteLine()
                            .WriteMarkupLine(ConsoleRender.FormatWarning("Agent self-check failed. Please resolve the issue as instructed and try again."))
                            .WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                    {
                        // User cancelled the operation, so we return to the read-line prompt.
                        continue;
                    }

                    Host.WriteErrorLine()
                        .WriteErrorMarkupLine($"Agent failed to generate a response: {ex.Message}")
                        .WriteErrorLine();
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
    }

    internal async Task RunOnceAsync(string prompt)
    {
        if (ActiveAgent is null)
        {
            string settingCommand = ConsoleRender.FormatInlineCode($"{Utils.AppName} --settings");
            string helpCommand = ConsoleRender.FormatInlineCode($"{Utils.AppName} --help");

            Host.WriteErrorMarkupLine($"No active agent was configured.");
            Host.WriteErrorMarkupLine($"Run {settingCommand} to configure the active agent. Run {helpCommand} for details.");

            return;
        }

        try
        {
            ShellProxy shellProxy = new(this);
            await ActiveAgent.Impl.Chat(prompt, shellProxy).WaitAsync(CancellationToken);
        }
        catch (OperationCanceledException)
        {
            Host.WriteErrorMarkupLine("Operation was aborted.");
        }
        catch (ShellCopilotException exception)
        {
            Host.WriteErrorMarkupLine(exception.Message.EscapeMarkup());
        }
    }
}
