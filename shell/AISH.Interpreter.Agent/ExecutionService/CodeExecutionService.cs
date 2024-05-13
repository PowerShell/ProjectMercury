namespace AISH.Interpreter.Agent;

/// <summary>
/// This class handles code exeuction on the local machine. All information 
/// generated in this class will be sent back using DataPackets
/// </summary>
public class CodeExecutionService
{
    private readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase) { "powershell", "python" };
    private readonly Dictionary<string, SubprocessLanguage> ActiveLanguages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> LangPathBools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// This method is used to run code on the local machine. It will return a DataPacket with the output of the code.
    /// </summary>
    /// <param name="language"></param>
    /// <param name="code"></param>
    public async Task<ToolResponsePacket> Run(string language, string code, CancellationToken token)
    {
        ToolResponsePacket packet = new(language, code);

        if (!TryGetLanguage(language, out SubprocessLanguage langObj))
        {
            packet.SetContent($"Language not supported.");
            return packet;
        }

        if (!LangPathBools[language])
        {
            packet.SetContent("Language not found on path.");
            return packet;
        }

        try
        {
            // outputQueue may be modified during enumeration. If an InvalidOperationException is thrown
            // a different solution than a delay may be needed.
            var outputQueue = await langObj.Run(code, token);

            foreach (OutputData outputItem in outputQueue)
            {
                switch (outputItem.Type)
                {
                    case OutputType.Error:
                        packet.SetError(true);
                        packet.SetContent(outputItem.Content + "\n");
                        break;
                    case OutputType.Output:
                        packet.SetContent(outputItem.Content + "\n");
                        break;
                    case OutputType.Interrupt:
                        throw new OperationCanceledException();
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
        foreach (var runningProcess in ActiveLanguages)
        {
            runningProcess.Value.Dispose();
        }
        ActiveLanguages.Clear();
    }

    public async Task<string> GetLanguageVersions()
    {
        // Get the version of each language
        string versions = "";
        foreach (string language in Languages)
        {
            if (TryGetLanguage(language, out SubprocessLanguage langObj))
            {
                // Check if the language is on the path during version check to avoid checking it again later.
                if (!LangPathBools.ContainsKey(language))
                {
                    LangPathBools.Add(language, langObj.IsOnPath());
                }

                if (LangPathBools[language])
                {
                    versions += $"- **{ language}**: { await langObj.GetVersion()}";
                }
                else
                {
                    versions += $"- **{language}**: Executable not found on PATH";
                }
            }
        }

        // Remove the languages from the active languages list to conserve memory.
        // Uncomment this code when more languages are added.
        // For now we only have two languages so we don't need to remove them.
        // foreach (string language in Languages)
        // {
        //     RemoveLanguage(language);
        // }

        return versions.Trim();
    }

    private bool TryGetLanguage(string language,out SubprocessLanguage langObj)
    {
        if (ActiveLanguages.TryGetValue(language, out langObj))
        {
            return true;
        }

        if (Languages.TryGetValue(language, out string actualName))
        {
            langObj = actualName switch
            {
                "powershell" => new PowerShell(),
                "python" => new Python(),
                _ => throw new NotSupportedException()
            };

            ActiveLanguages.Add(actualName, langObj);
            return true;
        }

        langObj = null;
        return false;
    }

    private void RemoveLanguage(string language)
    {
        if (ActiveLanguages.TryGetValue(language, out SubprocessLanguage langObj))
        {
            langObj.Dispose();
            ActiveLanguages.Remove(language);
        }
    }
}
