using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Feedback;
using System.Text;
using AIShell.Abstraction;

namespace AIShell.Integration;

public sealed class ErrorFeedback : IFeedbackProvider
{
    internal const string GUID = "10A13623-CE5E-4808-8346-1DEC831C24BB";

    private readonly Guid _guid;

    internal ErrorFeedback()
    {
        _guid = new Guid(GUID);
        SubsystemManager.RegisterSubsystem(SubsystemKind.FeedbackProvider, this);
    }

    Dictionary<string, string> ISubsystem.FunctionsToDefine => null;

    public Guid Id => _guid;

    public string Name => "AIShell";

    public string Description => "Provide feedback for errors by leveraging AI agents running in AIShell.";

    public FeedbackTrigger Trigger => FeedbackTrigger.Error;

    public FeedbackItem GetFeedback(FeedbackContext context, CancellationToken token)
    {
        // The trigger we listen to is 'Error', so 'LastError' won't be null.
        Channel channel = Channel.Singleton;
        if (channel.CheckConnection(blocking: false, out _))
        {
            string query = CreateQueryForError(context.CommandLine, context.LastError, channel);
            PostQueryMessage message = new(query, context: null, agent: null);
            channel.PostQuery(message);

            return new FeedbackItem(header: "Check the sidecar for suggestions from AI.", actions: null);
        }

        return null;
    }

    internal static string CreateQueryForError(string commandLine, ErrorRecord lastError, Channel channel)
    {
        Exception exception = lastError.Exception;
        StringBuilder sb = new StringBuilder(capacity: 100)
            .Append(
                $"""
                Running the command line `{commandLine}` in PowerShell v{channel.PSVersion} failed.
                Please try to explain the failure and suggest the right fix.
                The error details can be found below in the markdown format.
                """)
            .Append("\n\n")
            .Append("# Error Details\n")
            .Append("## Exception Messages\n")
            .Append($"{exception.GetType().FullName}: {exception.Message}\n");

        exception = exception.InnerException;
        if (exception is not null)
        {
            sb.Append("Inner Exceptions:\n");
            do
            {
                sb.Append($"  - {exception.GetType().FullName}: {exception.Message}\n");
                exception = exception.InnerException;
            }
            while (exception is not null);
        }

        string positionMessage = lastError.InvocationInfo?.PositionMessage;
        if (!string.IsNullOrEmpty(positionMessage))
        {
            sb.Append("## Error Position\n").Append(positionMessage).Append('\n');
        }

        return sb.ToString();
    }

    internal void Unregister()
    {
        SubsystemManager.UnregisterSubsystem<IFeedbackProvider>(_guid);
    }
}
