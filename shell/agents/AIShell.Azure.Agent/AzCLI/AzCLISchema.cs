using System.Text.Json.Serialization;

namespace AIShell.Azure.CLI;

internal class Query
{
    public List<ChatMessage> Messages { get; set; }
}

internal class CommandItem
{
    public string Desc { get; set; }
    public string Script { get; set; }
}

internal class PlaceholderItem
{
    public string Name { get; set; }
    public string Desc { get; set; }
    public string Type { get; set; }

    [JsonPropertyName("valid_values")]
    public List<string> ValidValues { get; set; }
}

internal class ResponseData
{
    public string Description { get; set; }
    public List<CommandItem> CommandSet { get; set; }
    public List<PlaceholderItem> PlaceholderSet { get; set; }
}

internal class AzCliResponse
{
    public int Status { get; set; }
    public string Error { get; set; }
    public ResponseData Data { get; set; }
}
