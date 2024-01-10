using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.CommandLine;

namespace ShellCopilot.Abstraction;

public abstract class CommandBase : Command, IDisposable
{
    private static readonly string[] s_helpAlias = new[] { "-h", "--help" };

    /// <summary>
    /// The constructor that a derived type has to implement.
    /// </summary>
    /// <param name="name">Name of the command.</param>
    /// <param name="description">Description of the command, which will be used to show help information.</param>
    protected CommandBase(string name, string description = null)
        : base(name, description)
    {
        _parser = null;
    }

    private Parser _parser;

    /// <summary>
    /// Gets the <see cref="IShell"/> implementation to interact with Shell Copilot.
    /// </summary>
    public IShell Shell { internal set; get; }

    /// <summary>
    /// Gets the source of the command.
    /// </summary>
    internal string Source { set; get; }

    /// <summary>
    /// Gets the parser to parse and invoke this command.
    /// </summary>
    internal Parser Parser =>
        _parser ??= new CommandLineBuilder(this)
            .UseHelp(s_helpAlias)
            .UseSuggestDirective()
            .UseTypoCorrections()
            .UseParseErrorReporting()
            .Build();

    /// <summary>
    /// Dispose the command.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The actual implementation to dispose the command.
    /// </summary>
    /// <param name="disposing">
    /// True indicates it's called by the <see cref="IDisposable.Dispose"/> method;
    /// False indicates it's called by the runtime from inside the finalizer.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        // The default implementation is non-op.
    }
}
