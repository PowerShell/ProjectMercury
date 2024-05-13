namespace AISH.Kernel;

public enum ExceptionHandlerAction
{
    Stop,
    Continue,
}

public sealed class AISHException : Exception
{
    public ExceptionHandlerAction HandlerAction { get; }

    public AISHException(string message)
        : this(message, ExceptionHandlerAction.Continue, innerException: null)
    {
    }

    public AISHException(string message, Exception innerException)
        : this(message, ExceptionHandlerAction.Continue, innerException)
    {
    }

    public AISHException(string message, ExceptionHandlerAction action, Exception innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        HandlerAction = action;
    }
}
