namespace ShellCopilot.Abstraction;

public enum UserAction
{
    /// <summary>
    /// Code was copied by user.
    /// </summary>
    CodeCopy,

    /// <summary>
    /// Code was saved by user.
    /// </summary>
    CodeSave,

    /// <summary>
    /// Code was inserted to the command-line shell by user.
    /// </summary>
    CodeInsert,

    /// <summary>
    /// User ran the 'like' command.
    /// </summary>
    Like,

    /// <summary>
    /// User ran the 'dislike' command.
    /// </summary>
    Dislike,
}

public abstract class UserActionPayload
{
    public UserAction Action { get; }

    protected UserActionPayload(UserAction action)
    {
        Action = action;
    }
}

public sealed class CodePayload : UserActionPayload
{
    public string Code { get; }

    public CodePayload(UserAction action, string code)
        : base(action)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        Code = code;
    }
}

public sealed class LikePayload : UserActionPayload
{
    public bool ShareConversation { get; }

    public LikePayload(bool share)
        : base(UserAction.Like)
    {
        ShareConversation = share;
    }
}

public sealed class DislikePayload : UserActionPayload
{
    public bool ShareConversation { get; }
    public string ShortFeedback { get; }
    public string LongFeedback { get; }

    public DislikePayload(bool share, string shortFeedback, string longFeedback)
        : base(UserAction.Dislike)
    {
        ShareConversation = share;
        ShortFeedback = shortFeedback;
        LongFeedback = longFeedback;
    }
}
