namespace ShellCopilot.Ollama.Agent;

internal class Query
{
    public string prompt { get; set; }
    public string model { get; set; }

    public bool stream { get; set; }
}

internal class Action
{
    public string Command { get; set; }
    public string Reason { get; set; }
    public string Example { get; set; }
    public List<string> Arguments { get; set; }
}

internal class ResponseData
{
    public string Scenario { get; set; }
    public string Description { get; set; }
    public List<Action> CommandSet { get; set; }
}

internal class OllamaResponse
{
    public int Status { get; set; }
    public string Error { get; set; }
    public string Api_version { get; set; }
    public List<ResponseData> Data { get; set; }
}
