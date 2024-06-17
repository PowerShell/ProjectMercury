---
external help file: ShellCopilot.Integration.dll-Help.xml
Module Name: Aish
online version:
ms.date: 06/21/2021
schema: 2.0.0
---

# Resolve-Error

## SYNOPSIS
Cmdlet to take the last error in the current session and send it to the open AISH window for
resolution.

## SYNTAX

```
Resolve-Error [-Agent <String>] [-IncludeOutputFromClipboard] [<CommonParameters>]
```

## DESCRIPTION

When an error occurs in the current session, this cmdlet sends the error to the AISH agent for
resolution. The command sends the full error object to the current AISH agent session, which
attempts to provide a resolution.

## EXAMPLES

### Example 1 - Resolves the last error

```powershell
PS> Start-AISH
#User receives an error

PS> Resolve-Error
```

This example shows how to ask AISH to resolve the last error that occurred in the current AISH
session. AISH analyzes the error and attempts to provide a solution in the AISH agent window.

## PARAMETERS

### -Agent

Specifies the agent to use in the current AISH session. If not specified, AISH uses the currently
selected agent.

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
to AISH.

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

## RELATED LINKS

[Invoke-Aish](Invoke-Aish.md)

[Start-Aish](Start-Aish.md)
