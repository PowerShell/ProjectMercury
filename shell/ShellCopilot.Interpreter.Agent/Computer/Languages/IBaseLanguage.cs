namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// Interface for the base language class. Every language class implements this interface for flexibility.
/// </summary>
internal interface IBaseLanguage
{
    /// <summary>
    /// Run the code and return the output in a DataPacket
    /// </summary>
    /// <param name="code"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    internal Task<Queue<Dictionary<string,string>>> Run(string code, CancellationToken token);

	/// <summary>
	/// Stops the process and cleans up any resources
	/// </summary>
	internal void Terminate();

	/// <summary>
	/// Checks to see if the language is on System path
	/// </summary>
	internal bool IsOnPath();

	/// <summary>
	/// Returns the version of the language executable on the user's local machine.
	/// </summary>
    internal Task<string> GetVersion();
}
