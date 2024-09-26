using System.Text.Json;

namespace Microsoft.Azure.Agent;

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

internal class TokenRequestException : Exception
{
    internal TokenRequestException(string message)
        : base(message)
    {
    }

    internal TokenRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal class ConnectionDroppedException : Exception
{
    internal ConnectionDroppedException(string message)
        : base(message)
    {
    }
}

internal class CorruptDataException : Exception
{
    internal CorruptDataException(string message)
        : base(message)
    {
    }

    internal static CorruptDataException Create(string message)
    {
        return new CorruptDataException($"Unexpected copilot activity received. {message}");
    }
}

internal class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
}
