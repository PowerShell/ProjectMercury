---
Module Name: AIShell
Module Guid: ecb8bee0-59b9-4dae-9d7b-a990b480279a
Download Help Link:
Help Version: 0.1.0
ms.date: 06/21/2021
Locale: en-US
---

# AIShell Module

## Description

This is a module to create a deeper connection between PowerShell 7 and AIShell. This module creates a
communication channel between PowerShell and AIShell to allow for sharing of information like queries,
errors and results from AIShell.

## AIShell Cmdlets

### [Invoke-AIShell](Invoke-AIShell.md)

When an AIShell session is available, this cmdlet sends a query to the AIShell window. Results are shown
in the AIShell window.

### [Resolve-Error](Resolve-Error.md)

Cmdlet to take the last error in the current session and send it to the open AIShell window for
resolution.

### [Start-AIShell](Start-AIShell.md)

Starts an AIShell session in a split pane window of Windows Terminal with a connected communication
channel with session that started it.
