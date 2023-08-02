namespace ShellCopilot.Kernel;

public enum ExceptionHandlerAction
{
    Stop,
    Continue,
}

public sealed class ShellCopilotException : Exception
{
    public ExceptionHandlerAction HandlerAction { get; }

    public ShellCopilotException(string message)
        : this(message, ExceptionHandlerAction.Continue, innerException: null)
    {
    }

    public ShellCopilotException(string message, ExceptionHandlerAction action, Exception innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        HandlerAction = action;
    }
}
