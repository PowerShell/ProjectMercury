using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace ShellCopilot.Kernel.Commands;

internal class CommandRunner
{
    private readonly Dictionary<string, Command> _commands;

    internal CommandRunner()
    {
        _commands = new(StringComparer.OrdinalIgnoreCase);
    }

    internal Dictionary<string, Command> Commands => _commands;

    internal void LoadBuiltInCommands(Shell shell)
    {
        _commands.Add("code", new CodeCommand(shell));
        _commands.Add("help", new HelpCommand(shell));
    }

    internal Command ResolveCommand(string name)
    {
        return _commands.TryGetValue(name, out Command value) ? value : null;
    }

    internal static Parser BuildParser(Command command)
    {
        var commandLineBuilder = new CommandLineBuilder(command);
        commandLineBuilder
            .UseHelp()
            .UseSuggestDirective()
            .UseTypoCorrections()
            .UseParseErrorReporting();
        return commandLineBuilder.Build();
    }

    internal void InvokeCommand(string commandLine)
    {
        int index = commandLine.IndexOf(' ');
        string commandName = index is -1 ? commandLine : commandLine[..index];

        Command command = ResolveCommand(commandName)
            ?? throw new ShellCopilotException($"The term '{commandName}' is not recognized as a name of a command.");

        Parser parser = BuildParser(command);
        parser.Invoke(commandLine);
    }
}
