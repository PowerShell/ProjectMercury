namespace AIShell.Ollama.Agent;

// Query class for the data to send to the endpoint
internal class Query
{
    public string prompt { get; set; }
    public string model { get; set; }
    public bool stream { get; set; }
}

// Response data schema
internal class ResponseData
{
    public string model { get; set; }
    public string created_at { get; set; }
    public string response { get; set; }
    public bool done { get; set; }
    public string done_reason { get; set; }
    public int[] context { get; set; }
    public double total_duration { get; set; }
    public long load_duration { get; set; }
    public int prompt_eval_count { get; set; }
    public int prompt_eval_duration { get; set; }
    public int eval_count { get; set; }
    public long eval_duration { get; set; }
}

internal class OllamaResponse
{
    public int Status { get; set; }
    public string Error { get; set; }
    public string Api_version { get; set; }
    public ResponseData Data { get; set; }
}
