---
external help file: ShellCopilot.Integration.dll-Help.xml
Module Name: Aish
online version:
schema: 2.0.0
---

# Invoke-Aish

## SYNOPSIS
When an AISH session is available, this cmdlet sends a query to the AISH agent and results are shown in the AISH window.

## SYNTAX

### Default (Default)
```
Invoke-Aish [-Query] <String> [-Agent <String>] [[-Context] <PSObject>] [<CommonParameters>]
```

### Clipboard
```
Invoke-Aish [-Query] <String> [-Agent <String>] [-ContextFromClipboard] [<CommonParameters>]
```

## DESCRIPTION
This cmdlet sends a query to the open AISH agent and results
## EXAMPLES

### Example 1
```powershell
PS C:\> Start-AISH
PS C:\> Invoke-AISH -Query "How do I list out the 5 most CPU intensive processes?"
```

This example sends a query, "How do I list out the 5 most CPU intensive processes?" to the AISH
agent. Responses are given in the AISH window.

## PARAMETERS

### -Agent
Which agent to use in the AISH window open. If not specified it will use the currently selected
agent.

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
Additional context you want to send to the AISH agent.

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
This shares the text content you have in your clipboard to the AISH agent.

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
The user input to send to the AISH agent.

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
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Management.Automation.PSObject
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
