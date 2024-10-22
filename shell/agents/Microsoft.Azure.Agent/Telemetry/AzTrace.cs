using System.Text.Json;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Microsoft.Azure.Agent;

public class AzTrace
{
    private static readonly string s_installationId;
    private static string GetInstallationID()
    {
        string azureConfigDir = Environment.GetEnvironmentVariable("AZURE_CONFIG_DIR");
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string userProfilePath = Path.Combine(string.IsNullOrEmpty(azureConfigDir) ? userProfile : azureConfigDir, "azureProfile.json");
        
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
            try
            {
                Path.Combine(string.IsNullOrEmpty(azureConfigDir) ? userProfile : azureConfigDir, "azureProfile.json");
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

    public string TopicName;
    // Each chat has a unique conversationId. When the cx runs /refresh,
    // a new chat is initiated(i.e.a new conversationId will be created).
    public string ConversationId;
    // The activity id of the user's query
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
