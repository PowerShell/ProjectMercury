namespace AIShell.Abstraction;

/// <summary>
/// The shell interface to interact with the AIShell.
/// </summary>
public interface IShell
{
    /// <summary>
    /// The host of the AIShell.
    /// </summary>
    IHost Host { get; }

    /// <summary>
    /// The token to indicate cancellation when `Ctrl+c` is pressed by user.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Utility method to assist parameter injection for a placeholder argument.
    /// </summary>
    /// <param name="argumentInfo">Information about the placeholder argument.</param>
    /// <returns>The real value to use provided by user.</returns>
    string AssistParameterInjection(ArgumentInfo argumentInfo);

    // TODO:
    // - methods to run code: python, command-line, powershell, node-js.
    // - methods to communicate with shell client.
}

/// <summary>
/// Information about an argument placeholder.
/// </summary>
public sealed class ArgumentInfo
{
    /// <summary>
    /// Type of the argument data.
    /// </summary>
    public enum DataType
    {
        String,
        Int,
        Bool,
    }

    /// <summary>
    /// Gets the placeholder name of the argument.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of the argument.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the restriction of the argument, such as allowed characters, length, etc.
    /// </summary>
    public string Restriction { get; }

    /// <summary>
    /// Gets the data type of the argument.
    /// </summary>
    public DataType Type { get; }

    /// <summary>
    /// Gets a value indicating whether the user must choose from the suggestions.
    /// </summary>
    public bool MustChooseFromSuggestions { get; }

    /// <summary>
    /// Gets the list of suggestions for the argument.
    /// </summary>
    public IList<string> Suggestions { get; }

    public ArgumentInfo(string name, string description, DataType type)
        : this(name, description, restriction: null, type, mustChooseFromSuggestions: false, suggestions: null)
    {
    }

    public ArgumentInfo(
        string name,
        string description,
        string restriction,
        DataType dataType,
        bool mustChooseFromSuggestions,
        IList<string> suggestions)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(description);

        if (mustChooseFromSuggestions && (suggestions is null || suggestions.Count < 2))
        {
            throw new ArgumentException(
                $"A suggestion list with at least 2 items is required when '{nameof(MustChooseFromSuggestions)}' is true.",
                nameof(suggestions));
        }

        Name = name;
        Description = description;
        Restriction = restriction;
        Type = dataType;
        MustChooseFromSuggestions = mustChooseFromSuggestions;
        Suggestions = suggestions;
    }
}
