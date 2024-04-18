namespace ShellCopilot.Interpreter.Agent;

    /// <summary>
    /// This class handles code exeuction on the local machine. All information 
    /// generated in this class will be sent back using DataPackets
    /// </summary>
public class CodeExecutionService
{
    private readonly List<string> Languages = ["powershell", "python"];
    private readonly Dictionary<string, SubprocessLanguage> ActiveLanguages = [];

    /// <summary>
    /// This method is used to run code on the local machine. It will return a DataPacket with the output of the code.
    /// </summary>
    /// <param name="language"></param>
    /// <param name="code"></param>
    public async Task<ToolResponsePacket> Run(string language, string code, CancellationToken token)
    {
        ToolResponsePacket packet = new(language, code);

        if (CheckAndAddLanguage(language) is false)
        {
            packet.SetContent("Language not supported.");
            return packet;
        }

        if (ActiveLanguages[language].IsOnPath() is false)
        {
            packet.SetContent("Language not found on path.");
            return packet;
        }
        try
        {
            foreach (Dictionary<string, string> outputItem in await ActiveLanguages[language].Run(code, token))
            {
                if (outputItem["type"] == "error")
                {
                    packet.SetError(true);
                    packet.SetContent(outputItem["content"] + "\n");
                }
                else if (outputItem["type"] == "output")
                {
                    packet.SetContent(outputItem["content"] + "\n");
                }
            }
        } 
        catch(OperationCanceledException)
        {
            packet.ResetContent("Code run cancelled.");
        }

        return packet;
    }

    public void Terminate()
    {
        foreach (KeyValuePair<string, SubprocessLanguage> runningProcess in ActiveLanguages)
        {
            runningProcess.Value.Terminate();
            RemoveLanguage(runningProcess.Key);
        }
    }

    private void RemoveLanguage(string language)
    {
        if (ActiveLanguages.ContainsKey(language))
        {
            ActiveLanguages[language].Terminate();
            ActiveLanguages.Remove(language);
        }
    }

    public async Task<string> GetLanguageVersions()
    {
        // Get the version of each language
        string versions = "";
        foreach (string language in Languages)
        {
            if (CheckAndAddLanguage(language))
            {
                versions += "- **" + language + "**: " + await ActiveLanguages[language].GetVersion();
            }
        }

        // Remove the languages from the active languages list
        foreach (string language in Languages)
        {
            RemoveLanguage(language);
        }
        return versions.Trim();
    }

    private bool CheckAndAddLanguage(string language)
    {
        if (Languages.Contains(language))
        {
            if (!ActiveLanguages.ContainsKey(language))
            {
                switch (language)
                {
                    case "powershell":
                        ActiveLanguages.Add(language, new PowerShell());
                        break;
                    case "python":
                        ActiveLanguages.Add(language, new Python());
                        break;
                }
            }
            return true;
        }
        else
        {
            return false;
        }
    }
}
