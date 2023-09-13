using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace ShellCopilot.Kernel.Commands;

internal class CommandRunner
{
    private readonly Dictionary<string, CommandBase> _commands;

    internal CommandRunner()
    {
        _commands = new(StringComparer.OrdinalIgnoreCase);
    }

    internal Dictionary<string, CommandBase> Commands => _commands;

    internal void LoadBuiltInCommands(Shell shell)
    {
        _commands.Add("code", new CodeCommand(shell));
        _commands.Add("help", new HelpCommand(shell));
        _commands.Add("exit", new ExitCommand(shell));
    }

    internal CommandBase ResolveCommand(string name)
    {
        return _commands.TryGetValue(name, out CommandBase value) ? value : null;
    }

    internal void InvokeCommand(string commandLine)
    {
        int index = commandLine.IndexOf(' ');
        string commandName = index is -1 ? commandLine : commandLine[..index];

        CommandBase command = ResolveCommand(commandName)
            ?? throw new ShellCopilotException($"The term '{commandName}' is not recognized as a name of a command.");

        command.Parser.Invoke(commandLine);
    }
}

internal abstract class CommandBase : Command
{
    private static readonly string[] s_helpAlias = new[] { "-h", "--help" };
    private Parser _parser;

    protected CommandBase(string name, string description = null)
        : base(name, description)
    {
        _parser = null;
    }

    internal Parser Parser
    {
        get
        {
            if (_parser is null)
            {
                var commandLineBuilder = new CommandLineBuilder(this);
                commandLineBuilder
                    .UseHelp(s_helpAlias)
                    .UseSuggestDirective()
                    .UseTypoCorrections()
                    .UseParseErrorReporting();
                _parser = commandLineBuilder.Build();
            }

            return _parser;
        }
    }
}
