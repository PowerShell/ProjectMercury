using System.Text.Json;

namespace AIShell.Azure;

internal static class Utils
{
    private static readonly JsonSerializerOptions s_jsonOptions;

    static Utils()
    {
        s_jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    internal static JsonSerializerOptions JsonOptions => s_jsonOptions;

    /// <summary>
    /// Keep 3 conversation iterations as the context information.
    /// </summary>
    internal const int HistoryCount = 6;
}

internal class RefreshTokenException : Exception
{
    internal RefreshTokenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
}
