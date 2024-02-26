using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Azure
{
    public class AzPSTrace
    {
        private bool? _enableAzureDataCollection = null;
        public string? Handler; // "Azure PowerShell / Azure CLI"
        public TimeSpan Duration;
        public DateTime StartTime;
        public DateTime EndTime;
        public Guid CorrelationId; // CorrelationId from client side.
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
        public Dictionary<string, string> ExtendedProperties;
        public AzPSTrace() { }
    }
}
