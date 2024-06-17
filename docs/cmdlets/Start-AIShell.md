---
external help file: AIShell.Integration.dll-Help.xml
Module Name: AIShell
online version:
ms.date: 06/21/2021
schema: 2.0.0
---

# Start-AIShell

## SYNOPSIS
Starts an AIShell session in a split pane window of Windows Terminal with a connected communication
channel to the PowerShell session that started it.

## SYNTAX

```
Start-AIShell [-Path <String>] [<CommonParameters>]
```

## DESCRIPTION

Starts an AIShell session in a split pane window of Windows Terminal and iTerm2. The AIShell session
is started in the right pane of the terminal window. The left pane is the current shell session. You
must use these windows to interact with the AIShell session.

## EXAMPLES

### Example 1 - Start an AIShell session

```powershell
Start-AIShell
```

### Example 2 - Start an AIShell session with a specific path

```powershell
Start-AIShell -PATH C:\Users\aish.exe
```

## PARAMETERS

### -Path

By default, the cmdlet looks for the `aish` executable in the locations listed in the `$env:PATH`
environment variable. Use this parameter to specify an alternate location for the `aish` executable.

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

[Invoke-AIShell](Invoke-AIShell.md)

[Resolve-Error](Resolve-Error.md)
