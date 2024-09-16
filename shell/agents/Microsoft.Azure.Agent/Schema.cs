using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Identity.Client;

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
    public string Text { get; set; }
    public string TopicName { get; set; }
    public string[] SuggestedUserResponses { get; set; }
    public ConversationState ConversationState { get; set; }
}
