using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIShell.Ollama.Agent;

internal class Settings
{
    public string Model { get; set; }
    public string Endpoint { get; set; }

    public Settings(ConfigData configData)
    {
        Model = configData.Model;
        Endpoint = configData.Endpoint;
    }
}

internal class ConfigData
{
    public string Model { get; set; }
    public string Endpoint { get; set; }
}

/// <summary>
/// Use source generation to serialize and deserialize the setting file.
/// Both metadata-based and serialization-optimization modes are used to gain the best performance.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ConfigData))]
internal partial class SourceGenerationContext : JsonSerializerContext { }
