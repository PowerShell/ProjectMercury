using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.Copilot
{
    [Alias("Copilot")]
    [Cmdlet(VerbsCommon.Enter, "Copilot")]
    public class RestoredEnterCopilot : Cmdlet
    {
        
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            new Microsoft.PowerShell.Copilot.EnterCopilot(true);
        }
    }

    
}