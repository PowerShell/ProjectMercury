using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Azure
{
    
    public class AzTrace
    {
        
        public static Guid GetInstallationID()
        {
            string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\")[1];
            string json = new StreamReader($"C:/Users/{userName}/.Azure/azureProfile.json").ReadToEnd();

            dynamic array = JsonConvert.DeserializeObject(json);

            return new Guid(array.installationId.Value);
        }
        
#nullable enable
        private bool? _enableAzureDataCollection = null;
        public string? Handler; // "Azure PowerShell / Azure CLI"
        public TimeSpan? Duration;
        public DateTime? StartTime;
        public DateTime? EndTime;
        public Guid? CorrelationID; // CorrelationId from client side.
        public Guid? InstallationID = GetInstallationID(); 
        public string? EventType;
        public string? Command;
        public string? Question; // Must be filtered
        public string? Answer; // Must be filtered
        /// <summary>
        /// Agent Information - may contain:
        /// Handler Version
        /// InstallationId
        /// Product Version
        /// .net/python Version
        /// </summary>
        public Dictionary<string, string>? ExtendedProperties;
        public AzTrace() { }
    }
}
