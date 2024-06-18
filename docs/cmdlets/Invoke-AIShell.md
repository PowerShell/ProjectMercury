---
external help file: AIShell.Integration.dll-Help.xml
Module Name: AIShell
online version:
ms.date: 06/17/2024
schema: 2.0.0
---

# Invoke-AIShell

## SYNOPSIS
When an AIShell session is available, this cmdlet sends a query to the AIShell window. Results are
shown in the AIShell window.

## SYNTAX

### Default (Default)

```
Invoke-AIShell [-Query] <String> [-Agent <String>] [[-Context] <PSObject>] [<CommonParameters>]
```

### Clipboard

```
Invoke-AIShell [-Query] <String> [-Agent <String>] [-ContextFromClipboard] [<CommonParameters>]
```

## DESCRIPTION

This cmdlet sends a query to the open AIShell agent and results are shown in the AIShell window.

## EXAMPLES

### Example 1 - Send a query to the AIShell agent

```powershell
Start-AIShell
Invoke-AIShell -Query "How do I list out the 5 most CPU intensive processes?"
```

This example sends a query, "How do I list out the 5 most CPU intensive processes?" to the AIShell
agent. Responses are given in the AIShell window.

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

### -Context

Additional context information you want to send to the AIShell agent.

```yaml
Type: System.Management.Automation.PSObject
Parameter Sets: Default
Aliases:

Required: False
Position: 1
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ContextFromClipboard

Use the content in your clipboard as context information for the AIShell agent.

```yaml
Type: System.Management.Automation.SwitchParameter
Parameter Sets: Clipboard
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Query

The user input to send to the AIShell agent.

```yaml
Type: System.String
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
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

### System.Management.Automation.PSObject

## OUTPUTS

### System.Object

## NOTES

PowerShell includes the following alias for this cmdlet:

- All platforms:
  - `askai`

## RELATED LINKS

[Start-AIShell](Start-AIShell.md)

[Resolve-Error](Resolve-Error.md)
