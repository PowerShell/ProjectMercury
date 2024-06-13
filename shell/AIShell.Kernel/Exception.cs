namespace AIShell.Kernel;

public enum ExceptionHandlerAction
{
    Stop,
    Continue,
}

public sealed class AIShellException : Exception
{
    public ExceptionHandlerAction HandlerAction { get; }

    public AIShellException(string message)
        : this(message, ExceptionHandlerAction.Continue, innerException: null)
    {
    }

    public AIShellException(string message, Exception innerException)
        : this(message, ExceptionHandlerAction.Continue, innerException)
    {
    }

    public AIShellException(string message, ExceptionHandlerAction action, Exception innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        HandlerAction = action;
    }
}
