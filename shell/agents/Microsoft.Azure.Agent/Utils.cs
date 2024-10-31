using System.Text.Encodings.Web;
using System.Text.Json;

namespace Microsoft.Azure.Agent;

internal static class Utils
{
    internal const string JsonContentType = "application/json";

    private static readonly JsonSerializerOptions s_jsonOptions;
    private static readonly JsonSerializerOptions s_humanReadableOptions;
    private static readonly JsonSerializerOptions s_relaxedJsonEscapingOptions;

    static Utils()
    {
        s_jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        s_humanReadableOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        s_relaxedJsonEscapingOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    internal static JsonSerializerOptions JsonOptions => s_jsonOptions;
    internal static JsonSerializerOptions JsonHumanReadableOptions => s_humanReadableOptions;
    internal static JsonSerializerOptions RelaxedJsonEscapingOptions => s_relaxedJsonEscapingOptions;

    internal async static Task EnsureSuccessStatusCodeForTokenRequest(this HttpResponseMessage response, string errorMessage)
    {
        if (!response.IsSuccessStatusCode)
        {
            string responseText = await response.Content.ReadAsStringAsync(CancellationToken.None);
            if (string.IsNullOrEmpty(responseText))
            {
                responseText = "<empty>";
            }

            string message = $"{errorMessage} HTTP status: {response.StatusCode}, Response: {responseText}.";
            Telemetry.Trace(AzTrace.Exception(message));
            throw new TokenRequestException(message);
        }
    }
}

internal class TokenRequestException : Exception
{
    /// <summary>
    /// Access to Copilot was denied.
    /// </summary>
    internal bool UserUnauthorized { get; set; }

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
    private CorruptDataException(string message)
        : base(message)
    {
    }

    internal static CorruptDataException Create(string message, CopilotActivity activity)
    {
        return new CorruptDataException($"Unexpected copilot activity received. {message}\n\n{activity.Serialize()}\n");
    }
}

internal class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
}
