namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// Interface for the base language class. Every language class implements this interface for flexibility.
/// </summary>
public interface IBaseLanguage
{
	/// <summary>
	/// Run the code and return the output in a DataPacket
	/// </summary>
	/// <param name="code"></param>
	/// <param name="language"></param>
	/// <returns></returns>
	public Task<Queue<Dictionary<string,string>>> Run(string code, CancellationToken token);

	/// <summary>
	/// Stops the process and cleans up any resources
	/// </summary>
	public void Terminate();

	/// <summary>
	/// Checks to see if the language is on System path
	/// </summary>
	public bool IsOnPath();
}
