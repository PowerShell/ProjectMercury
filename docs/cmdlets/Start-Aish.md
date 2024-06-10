---
external help file: ShellCopilot.Integration.dll-Help.xml
Module Name: Aish
online version:
schema: 2.0.0
---

# Start-Aish

## SYNOPSIS
Starts an AISH session in a split pane window of Windows Terminal with a connected communication
channel with session that started it.

## SYNTAX

```
Start-Aish [-Path <String>] [<CommonParameters>]
```

## DESCRIPTION
Starts and AISH Session in a split pane window of Windows Terminal. The AISH session is started in
the right pane of the terminal window. The left pane is the current session. This is required to
utilize all other commands.

## EXAMPLES

### Example 1
```powershell
PS C:\> Start-AISH
```

If aish.exe is found in the PATH, this will start an AISH session in the right pane of the terminal window.

### Example 
```powershell
PS C:\> Start-AISH -PATH C:\Users\aish.exe
```

This example specifies the path to a specific aish.exe file.

## PARAMETERS

### -Path
Optional path to the aish.exe file. If not specified, the PATH environment variable is used to find aish.exe.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
