---
external help file: AIShell.Integration.dll-Help.xml
Module Name: AIShell
online version:
ms.date: 06/17/2024
schema: 2.0.0
---

# Resolve-Error

## SYNOPSIS
Cmdlet to take the last error in the current session and send it to the open AIShell window for
resolution.

## SYNTAX

```
Resolve-Error [-Agent <String>] [-IncludeOutputFromClipboard] [<CommonParameters>]
```

## DESCRIPTION

When an error occurs in the current session, this cmdlet sends the error to the AIShell agent for
resolution. The command sends the full error object to the current AIShell agent session, which
attempts to provide a resolution.

## EXAMPLES

### Example 1 - Resolves the last error

```powershell
PS> Start-AIShell
#User receives an error

PS> Resolve-Error
```

This example shows how to ask AIShell to resolve the last error that occurred in the current AIShell
session. AIShell analyzes the error and attempts to provide a solution in the AIShell agent window.

## PARAMETERS

### -Agent

Specifies the agent to use in the current AIShell session. If not specified, AIShell uses the
currently selected agent.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeOutputFromClipboard

When this parameter is specified, the output copied to the clipboard is included in the error sent
to AIShell.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose,
-WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### System.Object

## NOTES

PowerShell includes the following alias for this cmdlet:

- All platforms:
  - `fixit`

## RELATED LINKS

[Invoke-AIShell](Invoke-AIShell.md)

[Start-AIShell](Start-AIShell.md)
