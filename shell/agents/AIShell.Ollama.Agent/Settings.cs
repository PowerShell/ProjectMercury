using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIShell.Ollama.Agent;

internal class Settings
{
    private string _model;
    private string _endpoint;
    private bool _stream;

    public string Model => _model;
    public string Endpoint => _endpoint;
    public bool Stream => _stream;

    public Settings(ConfigData configData)
    {
        _model = configData?.Model;
        _endpoint = configData?.Endpoint?.TrimEnd('/');
        _stream = configData?.Stream ?? false;
    }
}

internal class ConfigData
{
    public string Model { get; set; }
    public string Endpoint { get; set; }
    public bool Stream { get; set; }
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
