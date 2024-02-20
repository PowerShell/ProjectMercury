using System.Text.Json;

namespace ShellCopilot.AzCLI.Agent;

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
}
