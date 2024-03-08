using System.Text.Json;

namespace ShellCopilot.Azure
{
    public class AzTrace
    {
        public static string GetInstallationID()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var json = new StreamReader(Environment.ExpandEnvironmentVariables($"{userProfile}/.Azure/azureProfile.json")).BaseStream; // .ReadToEnd();
            var array = JsonSerializer.Deserialize<JsonElement>(json);
            
            return array.GetProperty("installationId").GetString();
        }

        // "Azure PowerShell / Azure CLI"
        public string Handler;
        // CorrelationId from client side.
        public string CorrelationID; 
        // private bool _enableAzureDataCollection = null;
        public TimeSpan? Duration;
        public DateTime? StartTime;
        public DateTime? EndTime;
        public string InstallationID;
        public string EventType;
        public string Command;
        /// <summary>
        /// Detailed information containing additional Information - may contain:
        /// Reason of dislike
        /// </summary>
        public string DetailedMessage;
        internal List<HistoryMessage> HistoryMessage;
        /// <summary>
        /// Agent Information - may contain:
        /// Handler Version
        /// Product Version
        /// .net/python Version
        /// </summary>
        public Dictionary<string, string> ExtendedProperties;
        public AzTrace() {}
    }

    // TODO: inherit from ChatMessage in PSSchema
    internal class HistoryMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public string CorrelationID { get; set; }
    }
}
