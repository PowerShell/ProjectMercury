﻿namespace AIShell.Abstraction;

public interface IHost
{
    /// <summary>
    /// Write out the string literally to stdout.
    /// </summary>
    IHost Write(string value);

    /// <summary>
    /// Write out a new line to to stdout.
    /// </summary>
    IHost WriteLine();

    /// <summary>
    /// Write out the string literally to stdout with a new line.
    /// </summary>
    IHost WriteLine(string value);

    /// <summary>
    /// Write out a new line to to stderr.
    /// </summary>
    IHost WriteErrorLine();

    /// <summary>
    /// Write out the error string to stderr with a new line.
    /// Format the error string in red color when stderr is not redirected.
    /// </summary>
    IHost WriteErrorLine(string value);

    /// <summary>
    /// Write out the markup string to stdout.
    /// </summary>
    IHost Markup(string value);

    /// <summary>
    /// Write out the markup string to stdout with a new line.
    /// </summary>
    IHost MarkupLine(string value);

    /// <summary>
    /// Write out a note with the passed-in markup string.
    /// </summary>
    IHost MarkupNoteLine(string value);

    /// <summary>
    /// Write out a warning with the passed-in markup string.
    /// </summary>
    IHost MarkupWarningLine(string value);

    /// <summary>
    /// Create a new instance of the <see cref="IStreamRender"/>.
    /// If the stdout is redirected, the returned render will simply write the raw chunks out.
    /// </summary>
    /// <param name="cancellationToken">Token to indicate cancellation.</param>
    IStreamRender NewStreamRender(CancellationToken cancellationToken);

    /// <summary>
    /// Render a full response to console output.
    ///  - if stdout is redirected, the raw response will be written out to stdout;
    ///  - otherwise, response will be parsed as markdown and rendered with rich UI elements to stdout.
    /// </summary>
    void RenderFullResponse(string response);

    /// <summary>
    /// Render the passed-in objects in the table format, for all public readable properties of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Type of the passed-in object.</typeparam>
    /// <param name="sources">Passed-in objects.</param>
    void RenderTable<T>(IList<T> sources);

    /// <summary>
    /// Render the passed-in objects in the table format.
    /// </summary>
    /// <typeparam name="T">Type of the passed-in object.</typeparam>
    /// <param name="sources">Passed-in objects.</param>
    /// <param name="elements">Header and value pairs to be rendered for each object.</param>
    void RenderTable<T>(IList<T> sources, IList<IRenderElement<T>> elements);

    /// <summary>
    /// Render the passed-in object out in the list format, for all public readable properties of <typeparamref name="T"/>,
    /// or for all key/value pairs if <typeparamref name="T"/> implements <see cref="IDictionary{String, String}"/>.
    /// </summary>
    /// <typeparam name="T">Type of the passed-in object.</typeparam>
    /// <param name="source">Passed-in object.</param>
    void RenderList<T>(T source);

    /// <summary>
    /// Render the passed-in object out in the list format.
    /// </summary>
    /// <typeparam name="T">Type of the passed-in object.</typeparam>
    /// <param name="source">Passed-in object.</param>
    /// <param name="elements">Label and value pairs to be rendered for the object.</param>
    void RenderList<T>(T source, IList<IRenderElement<T>> elements);

    /// <summary>
    /// Run an asynchronouse task with a spinner on the console showing the task in progress.
    /// </summary>
    /// <typeparam name="T">The return type of the asynchronouse task.</typeparam>
    /// <param name="func">The asynchronouse task.</param>
    /// <param name="status">The status message to be shown.</param>
    /// <returns>The returned result of <paramref name="func"/></returns>
    Task<T> RunWithSpinnerAsync<T>(Func<Task<T>> func, string status);

    /// <summary>
    /// Run an asynchronouse task with a spinner on the console showing the task in progress.
    /// Allow changing the status message from within the task while the spinner is running.
    /// </summary>
    /// <typeparam name="T">The return type of the asynchronouse task.</typeparam>
    /// <param name="func">The asynchronouse task, which can change the status of the spinner.</param>
    /// <param name="status">The initial status message to be shown.</param>
    /// <returns>The returned result of <paramref name="func"/></returns>
    Task<T> RunWithSpinnerAsync<T>(Func<IStatusContext, Task<T>> func, string status);

    /// <summary>
    /// Run an asynchronouse task with a spinner with the default status message.
    /// </summary>
    Task<T> RunWithSpinnerAsync<T>(Func<Task<T>> func) => RunWithSpinnerAsync(func, "Generating...");

    /// <summary>
    /// Prompt for selection asynchronously.
    /// </summary>
    /// <param name="title">Title of the prompt.</param>
    /// <param name="choices">Objects to be listed as the choices.</param>
    /// <param name="converter">Lambda to convert a choice object to a string.</param>
    /// <param name="cancellationToken">Token to cancel operation.</param>
    /// <returns>The chosen object.</returns>
    Task<T> PromptForSelectionAsync<T>(string title, IEnumerable<T> choices, Func<T, string> converter, CancellationToken cancellationToken);

    /// <summary>
    /// Prompt for selection asynchronously with the default to-string converter.
    /// </summary>
    /// <param name="title">Title of the prompt.</param>
    /// <param name="choices">Objects to be listed as the choices.</param>
    /// <param name="cancellationToken">Token to cancel operation.</param>
    /// <returns>The chosen object.</returns>
    Task<T> PromptForSelectionAsync<T>(string title, IEnumerable<T> choices, CancellationToken cancellationToken)
        => PromptForSelectionAsync(title, choices, converter: null, cancellationToken);

    /// <summary>
    /// Prompt for confirmation asynchronously.
    /// </summary>
    Task<bool> PromptForConfirmationAsync(string prompt, bool defaultValue, CancellationToken cancellationToken);

    /// <summary>
    /// Prompt for secret asynchronously.
    /// </summary>
    /// <param name="prompt">The prompt to use.</param>
    /// <param name="cancellationToken">Token to cancel operation.</param>
    Task<string> PromptForSecretAsync(string prompt, CancellationToken cancellationToken);

    /// <summary>
    /// Prompt for text input asynchronously.
    /// </summary>
    /// <param name="prompt">The prompt to use.</param>
    /// <param name="optional">Indicates if the text input request is optional.</param>
    /// <param name="choices">The choices for the user to choose from.</param>
    /// <param name="cancellationToken">Token to cancel operation.</param>
    /// <returns></returns>
    Task<string> PromptForTextAsync(string prompt, bool optional, IList<string> choices, CancellationToken cancellationToken);

    /// <summary>
    /// Prompt for text input asynchronously with no choice provided.
    /// </summary>
    /// <param name="prompt">The prompt to use.</param>
    /// <param name="optional">Indicates if the text input request is optional.</param>
    /// <param name="cancellationToken">Token to cancel operation.</param>
    /// <returns></returns>
    Task<string> PromptForTextAsync(string prompt, bool optional, CancellationToken cancellationToken)
        => PromptForTextAsync(prompt, optional, choices: null, cancellationToken);

    /// <summary>
    /// Prompt for the user to input the value for an argument placeholder.
    /// </summary>
    /// <param name="argInfo">Information about the argument placeholder.</param>
    /// <param name="cancellationToken">Token to cancel operation.</param>
    /// <returns></returns>
    string PromptForArgument(ArgumentInfo argInfo, CancellationToken cancellationToken);
}

/// <summary>
/// Interface for the status context used when displaying a spinner.
/// </summary>
public interface IStatusContext
{
    /// <summary>
    /// Sets the new status.
    /// </summary>
    void Status(string status);
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

    public ArgumentInfo(string name, string description, DataType dataType)
        : this(name, description, restriction: null, dataType, mustChooseFromSuggestions: false, suggestions: null)
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
