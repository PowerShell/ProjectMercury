using System.Text.Json;

namespace Microsoft.Azure.Agent;

public class AzTrace
{
    private static readonly string s_installationId;
    private static string GetInstallationID()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string userProfilePath = Path.Combine(userProfile, ".Azure", "azureProfile.json");
        FileStream jsonStream;
        JsonElement array;
        string installationId;

        if (File.Exists(userProfilePath))
        {
            jsonStream = new FileStream(userProfilePath, FileMode.Open, FileAccess.Read);
            array = JsonSerializer.Deserialize<JsonElement>(jsonStream);
            installationId = array.GetProperty("installationId").GetString();
        }
        else
        {
            userProfilePath = Path.Combine(userProfile, ".Azure", "AzureRmContextSettings.json");
            try
            {
                jsonStream = new FileStream(userProfilePath, FileMode.Open, FileAccess.Read);
                array = JsonSerializer.Deserialize<JsonElement>(jsonStream);
                installationId = array.GetProperty("Settings").GetProperty("InstallationId").GetString();
            }
            catch
            {
                // If finally no installation id found, just return null.
                return null;
            }
        }

        return installationId;
    }

    public string Handler;
    // CorrelationId from client side.
    public string CorrelationId;
    public string ActivityId;
    public string InstallationId = s_installationId;
    public string EventType;
    public string Command;
    /// <summary>
    /// Detailed information containing additional Information - may contain:
    /// Reason of dislike
    /// </summary>
    public string DetailedMessage;
    /// <summary>
    /// Agent Information - may contain:
    /// Handler Version
    /// Product Version
    /// .net/python Version
    /// </summary>
    public Dictionary<string, string> ExtendedProperties;
    static AzTrace() => s_installationId = GetInstallationID();
}

// TODO: inherit from ChatMessage in PSSchema
internal class HistoryMessage
{
    internal HistoryMessage(string role, string content, string correlationId)
    {
        Role = role;
        Content = content;
        CorrelationId = correlationId;
    }

    public string Role { get; }
    public string Content { get; }
    public string CorrelationId { get; }
}
