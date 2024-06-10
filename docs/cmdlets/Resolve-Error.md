---
external help file: ShellCopilot.Integration.dll-Help.xml
Module Name: Aish
online version:
schema: 2.0.0
---

# Resolve-Error

## SYNOPSIS
Cmdlet to take the last error in the current session and send it to the open AISH window for resolution.

## SYNTAX

```
Resolve-Error [-Agent <String>] [-IncludeOutputFromClipboard] [<CommonParameters>]
```

## DESCRIPTION
After an error occurs in the current session, this cmdlet sends the error to the AISH agent for
resolution. The full error object will be sent to the AISH agent open and attempt to resolve.

## EXAMPLES

### Example 1
```powershell
PS C:\> Start-AISH
<User hits an error>
PS C:\> Resolve-Error
```

This sends the last error hit to the current AISH window to attempt to resolve.

## PARAMETERS

### -Agent
A way to specify which agent to use in the AISH window open. If not specified it will use the
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
If this parameter is specified, the output from the clipboard will be included in the error sent.

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
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
