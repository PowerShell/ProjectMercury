namespace ShellCopilot.Interpreter.Agent;

    /// <summary>
    /// This class acts as a computer object that can be used to execute code on the local machine. All information 
    /// generated in this class will be sent back using DataPackets
    /// </summary>
public class Computer
{
    private List<string> Languages = ["powershell", "python"];
    private Dictionary<string, IBaseLanguage> ActiveLanguages = [];

    public Computer()
    {
    }

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
            foreach (Dictionary<string, string> outputItem in await ActiveLanguages[language].Run(code))
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
            throw;
        }

        return packet;
    }

    public void Terminate()
    {
        foreach (KeyValuePair<string, IBaseLanguage> runningProcess in ActiveLanguages)
        {
            runningProcess.Value.Terminate();
        }
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
