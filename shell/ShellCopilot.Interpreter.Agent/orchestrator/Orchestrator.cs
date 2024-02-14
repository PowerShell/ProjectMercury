using ShellCopilot.Abstraction;


namespace ShellCopilot.Interpreter.Agent;

public class Orchestrator
{
    private IShell _shell { get; set; }
    public IDictionary<string,string> _codeblocks { get; set; }
    private readonly CancellationToken token;
    

	public Orchestrator(string responseContent, IShell shell)
	{
        _codeblocks = new Dictionary<string, string>();
        _shell = shell;
        token = shell.CancellationToken;
        ExtractCodeFromResponse(responseContent);
	}
    public async Task<string> RunCode(string language, string code)
    {
        string[] codeOutput;
        switch (language)
        {
            case "python":
                // Create a new python object and send the code to it
                Python python = new(code);
                // Run the code and get the output
                codeOutput = await python.Run();
                // If there was an error, print it out and ask chatGPT to fix it
                if (codeOutput[0] == "error")
                {
                    string errorMessage = "Error: I'm getting the following error when I try to run the code:\n"
                                           + codeOutput[1] + " please rewrite the code with the fixes.";
                    return errorMessage;
                }
                else
                {
                    string outputMessage = language + ":\n\n" + codeOutput[1];
                    return outputMessage;
                }
            case "powershell":
                // Create a new powershell object and send the code to it
                PowerShell powershell = new(code);
                // Run the code and get the output
                codeOutput = await powershell.Run();
                // If there was an error, print it out and ask chatGPT to fix it
                if (codeOutput[0] == "error")
                {
                    string errorMessage = "Error: I'm getting the following error when I try to run the code:\n"
                                           + codeOutput[1] + " please rewrite the code with the fixes.";
                    return errorMessage;
                }
                else
                {
                    string outputMessage = language + ":\n\n" + codeOutput[1];
                    return outputMessage;
                }
            default:
                string unsupportedMessage = $"{language} is not supported at this time.";
                return unsupportedMessage;
        }
    }

    private void ExtractCodeFromResponse(string responseContent)
    {
        int startIndex = responseContent.IndexOf("```");
        int endIndex = responseContent.IndexOf("```", startIndex + 3);

        // Exit if no code block found
        while (startIndex != -1 && endIndex != -1)
        {
            // Find the first set of backticks
            string codeBlockContent = responseContent.Substring(startIndex + 3, endIndex - startIndex - 3);
            // Exit if code block is empty
            if (string.IsNullOrEmpty(codeBlockContent))
            {
                continue;
            }
            else
            {
                string language = GetCodeLanguage(codeBlockContent);
                _codeblocks.Add(language, codeBlockContent);
            }
            startIndex = responseContent.IndexOf("```", endIndex + 3);
            endIndex = responseContent.IndexOf("```", startIndex + 3);
        }
    }

#pragma warning disable CA1822 // Mark members as static
    private string GetCodeLanguage(string codeBlock)
#pragma warning restore CA1822 // Mark members as static
    {
        string codeBlockName = codeBlock.Substring(0, codeBlock.IndexOf('\n'));
        if (codeBlockName.Length > 0)
        {
            return codeBlockName;
        }
        else
        {
            return "";
        }
    }

    public void ResetCodeBlocks()
    {
        _codeblocks.Clear();
    }
}
