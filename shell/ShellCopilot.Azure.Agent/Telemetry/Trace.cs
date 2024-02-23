using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShellCopilot.Azure.Agent.Telemetry;

namespace ShellCopilot.Azure.Agent.Telemetry
{
    public class AzPSTrace
    {
        private bool? _enableAzureDataCollection = null;
        public string? CommandType;
        public AzPSTrace() { }
    }
}
