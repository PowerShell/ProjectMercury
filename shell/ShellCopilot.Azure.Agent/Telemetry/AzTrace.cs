using ShellCopilot.Azure.PowerShell;
using System.Text.Json;

namespace ShellCopilot.Azure
{
    public class AzTrace
    {
        public static string GetInstallationID()
        {
            string json = new StreamReader(Environment.ExpandEnvironmentVariables(@"%USERPROFILE%/.Azure/azureProfile.json")).ReadToEnd();
            var array = JsonSerializer.Deserialize<JsonElement>(json);
            
            return array.GetProperty("installationId").GetString();
        }

        public string Handler; // "Azure PowerShell / Azure CLI"
        public string CorrelationID = Guid.NewGuid().ToString(); // CorrelationId from client side.

        // private bool _enableAzureDataCollection = null;
        public TimeSpan? Duration;
        public DateTime? StartTime;
        public DateTime? EndTime;
        public string? InstallationID = GetInstallationID();
        public Guid? SubscriptionID;
        public Guid? TenantID;
        public string? EventType;
        public string? Command;
        /// <summary>
        /// Detailed information containing additional Information - may contain:
        /// Reason of dislike
        /// </summary>
        public string? DetailedMessage;
        internal List<HistoryMessage> HistoryMessage = [];
        /// <summary>
        /// Agent Information - may contain:
        /// Handler Version
        /// Product Version
        /// .net/python Version
        /// </summary>
        public Dictionary<string, string>? ExtendedProperties;
        public AzTrace() { }
    }

    // TODO: inherit from ChatMessage in PSSchema
    internal class HistoryMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public string CorrelationID { get; set; }
    }
}
