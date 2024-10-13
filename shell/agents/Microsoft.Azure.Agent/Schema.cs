using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AIShell.Abstraction;

namespace Microsoft.Azure.Agent;

internal class TokenPayload
{
    public string ConversationId { get; set; }
    public string Token { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

internal class SessionPayload
{
    public string ConversationId { get; set; }
    public string Token { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    public string StreamUrl { get; set; }
    public string ReferenceGrammarId { get; set; }
}

/// <summary>
/// Interface for copilot channel account.
/// </summary>
internal class ChannelAccount
{
    public string Id { get; set; }
    public string Name { get; set; }
};

/// <summary>
/// Interface for copilot activity data.
/// </summary>
internal class CopilotActivity
{
    public const string ConversationStateName = "azurecopilot/conversationstate";
    public const string SuggestedResponseName = "azurecopilot/suggesteduserresponses";
    public const string CLIHandlerTopic = "CLIHandler";

    public string Type { get; set; }
    public string Id { get; set; }
    public string Timestamp { get; set; }
    public string ChannelId { get; set; }
    public ChannelAccount From { get; set; }
    public string TopicName { get; set; }
    public string Text { get; set; }
    public string InputHint { get; set; }
    public JsonObject[] Attachments { get; set; }
    public string ReplyToId { get; set; }

    internal Exception Error { get; set; }

    internal bool IsFromCopilot => From.Id.StartsWith("copilot", StringComparison.OrdinalIgnoreCase);
    internal bool IsTyping => Type is "typing";
    internal bool IsEvent => Type is "event";
    internal bool IsMessage => Type is "message";
    internal bool IsMessageUpdate => Type is "messageUpdate";

    internal void ExtractMetadata(out string[] suggestedUserResponses, out ConversationState conversationState)
    {
        suggestedUserResponses = null;
        conversationState = null;

        foreach (JsonObject jObj in Attachments)
        {
            string name = jObj["name"].ToString();
            JsonNode content = jObj["content"];

            if (name is CopilotActivity.SuggestedResponseName)
            {
                suggestedUserResponses = content.Deserialize<string[]>(Utils.JsonOptions);
            }
            else if (name is CopilotActivity.ConversationStateName)
            {
                conversationState = content.Deserialize<ConversationState>(Utils.JsonOptions);
            }
        }
    }

    internal string Serialize() => JsonSerializer.Serialize(this, Utils.JsonHumanReadableOptions);
};

internal class ConversationState
{
    public int TurnLimit { get; set; }
    public int TurnNumber { get; set; }
    public bool TurnLimitMet { get; set; }
    public bool TurnLimitExceeded { get; set; }

    public int DailyConversationNumber { get; set; }
    public bool DailyConversationLimitMet { get; set; }
    public bool DailyConversationLimitExceeded { get; set; }
}

internal class CopilotResponse
{
    internal CopilotResponse(ChunkReader chunkReader, string topicName)
    {
        ArgumentNullException.ThrowIfNull(chunkReader);
        ArgumentException.ThrowIfNullOrEmpty(topicName);

        ChunkReader = chunkReader;
        TopicName = topicName;
    }

    internal CopilotResponse(string text, string topicName)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentException.ThrowIfNullOrEmpty(topicName);

        Text = text;
        TopicName = topicName;
    }

    internal ChunkReader ChunkReader { get; }
    internal string Text { get; }
    internal string TopicName { get; }
    internal string[] SuggestedUserResponses { get; set; }
    internal ConversationState ConversationState { get; set; }
}

internal class RawResponse
{
    public List<CopilotActivity> Activities { get; set; }
    public string Watermark { get; set; }
}

internal class ChunkReader
{
    private readonly string _replyToId;
    private readonly AzureCopilotReceiver _receiver;
    private CopilotActivity _current;
    private bool _complete;

    internal ChunkReader(AzureCopilotReceiver receiver, CopilotActivity current)
    {
        _replyToId = current.ReplyToId;
        _receiver = receiver;
        _current = current;
        _complete = false;
    }

    internal CopilotActivity ReadChunk(CancellationToken cancellationToken)
    {
        if (_current is not null)
        {
            CopilotActivity ret = _current;
            _current = null;
            return ret;
        }

        if (_complete)
        {
            return null;
        }

        CopilotActivity activity = _receiver.Take(cancellationToken);

        if (!activity.IsMessageUpdate)
        {
            throw CorruptDataException.Create($"The 'type' should be 'messageUpdate' but it's '{activity.Type}'.", activity);
        }
        if (activity.ReplyToId != _replyToId)
        {
            throw CorruptDataException.Create($"The 'replyToId' should be '{_replyToId}', but it's '{activity.ReplyToId}'.", activity);
        }
        if (activity.InputHint is not "typing" and not "acceptingInput")
        {
            throw CorruptDataException.Create($"The 'inputHint' should be 'typing' or 'acceptingInput', but it's '{activity.InputHint}'.", activity);
        }

        _complete = activity.InputHint is "acceptingInput";

        return activity;
    }
}

internal class CommandItem
{
    internal SourceInfo SourceInfo { get; set; }
    internal string Script { get; set; }
}

internal class PlaceholderItem
{
    public string Name { get; set; }
    public string Desc { get; set; }
    public string Type { get; set; }
    public List<string> ValidValues { get; set; }
}

internal class ResponseData
{
    internal string Text { get; set; }
    internal List<CommandItem> CommandSet { get; set; }
    internal List<PlaceholderItem> PlaceholderSet { get; set; }
}

internal class ArgumentPlaceholder
{
    internal ArgumentPlaceholder(string query, ResponseData data)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        ArgumentNullException.ThrowIfNull(data);

        Query = query;
        ResponseData = data;
        DataRetriever = new(data);
    }

    public string Query { get; set; }
    public ResponseData ResponseData { get; set; }
    public DataRetriever DataRetriever { get; }
}
