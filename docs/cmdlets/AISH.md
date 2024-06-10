---
Module Name: AISH
Module Guid: ecb8bee0-59b9-4dae-9d7b-a990b480279a
Download Help Link: 
Help Version: 0.1.0
Locale: en-US
---

# AISH Module
## Description
This is a module to create a deeper connection between PowerShell 7 and AISH. This module creates a
communication channel between PowerShell and AISH to allow for sharing of information like queries,
errors and results from AISH.

## AISH Cmdlets
### [Invoke-Aish](Invoke-Aish.md)
When an AISH session is available, this cmdlet sends a query to the AISH window. Results are shown in the AISH window.

### [Resolve-Error](Resolve-Error.md)
Cmdlet to take the last error in the current session and send it to the open AISH window for resolution.

### [Start-Aish](Start-Aish.md)
Starts an AISH session in a split pane window of Windows Terminal with a connected communication
channel with session that started it.

