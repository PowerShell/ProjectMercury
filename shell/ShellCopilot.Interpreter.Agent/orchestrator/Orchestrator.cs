using ShellCopilot.Abstraction;


namespace ShellCopilot.Interpreter.Agent;

public class Orchestrator
{
    private IShell _shell { get; set; }
    public KeyValuePair<string,string> CodeBlock { get; set; }
    private readonly CancellationToken token;
    private int _accumaltedResponseCursor = 0;
    private Python _python;
    private PowerShell _powershell;
    

	public Orchestrator(IShell shell)
	{
        CodeBlock = new KeyValuePair<string, string>();
        _shell = shell;
        token = shell.CancellationToken;
	}
    public async Task<string> RunCode(string language, string code)
    {
        string[] codeOutput;
        switch (language)
        {
            case "python":
                // Create a new python object and send the code to it
                if(_python == null)
                {
                    _python = new(code);
                }
                else
                {
                    _python.AppendToTempFile(code);
                }
                // Run the code and get the output
                codeOutput = await _python.Run();
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
            case "bash":
                PowerShell BashShell = new(language, code);
                codeOutput = await BashShell.Run();
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

    public bool IsCodeBlockComplete(string responseContent)
    {
        bool isCodeBlockComplete = false;
        if (responseContent.Contains("```"))
        {
            isCodeBlockComplete = ExtractCodeFromResponse(responseContent);
        }
        return isCodeBlockComplete;
    }

    private bool ExtractCodeFromResponse(string responseContent)
    {
        bool isCodeExtracted = false;
        int startIndex;
        int endIndex;
        if(_accumaltedResponseCursor == 0)
        {
            startIndex = responseContent.IndexOf("```");
            endIndex = responseContent.IndexOf("```", startIndex + 3);
        }
        else
        {
            startIndex = responseContent.IndexOf("```", _accumaltedResponseCursor);
            endIndex = responseContent.IndexOf("```", startIndex + 3);
        }
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
                if(language.Length == 0)
                {
                    language = "powershell";
                }
                CodeBlock = new KeyValuePair<string,string>(language, codeBlockContent);
                isCodeExtracted = true;
                _accumaltedResponseCursor = endIndex + 3;
                break;
            }
        }
        return isCodeExtracted;
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
}
