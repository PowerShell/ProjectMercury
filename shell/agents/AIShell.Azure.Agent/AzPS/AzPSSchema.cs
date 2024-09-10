using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIShell.Azure.PowerShell;

internal class Query
{
    public List<ChatMessage> Messages { get; set; }

    [JsonPropertyName("is_streaming")]
    public bool IsStreaming { get; set; }
}

internal class ChunkData
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }
    public double Created { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
}

internal class ChunkReader : IDisposable
{
    private readonly StreamReader _reader;
    private ChunkData _current;

    internal ChunkReader(StreamReader reader, ChunkData currentChunk)
    {
        _reader = reader;
        _current = currentChunk;
    }

    internal async Task<ChunkData> ReadChunkAsync(CancellationToken cancellationToken)
    {
        if (_current is not null)
        {
            ChunkData ret = _current;
            _current = null;
            return ret;
        }

        string line = await _reader.ReadLineAsync(cancellationToken);
        return line is null ? null : JsonSerializer.Deserialize<ChunkData>(line, Utils.JsonOptions);
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}
