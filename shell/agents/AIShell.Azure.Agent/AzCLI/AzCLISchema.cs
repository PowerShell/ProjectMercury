namespace AIShell.Azure.CLI;

internal class Query
{
    public string Question { get; set; }
    public List<ChatMessage> History { get; set; }
    public int Top_num { get; set; }
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

internal class AzCliResponse
{
    public int Status { get; set; }
    public string Error { get; set; }
    public string Api_version { get; set; }
    public List<ResponseData> Data { get; set; }
}
