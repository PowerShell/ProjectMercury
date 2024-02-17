using System.Text.Json;

namespace ShellCopilot.AzPS.Agent;

internal static class Utils
{
    private static readonly JsonSerializerOptions s_jsonOptions;

    static Utils()
    {
        s_jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    internal static JsonSerializerOptions JsonOptions => s_jsonOptions;
}
