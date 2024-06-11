---
external help file: ShellCopilot.Integration.dll-Help.xml
Module Name: Aish
online version:
ms.date: 06/21/2021
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

Starts an AISH session in a split pane window of Windows Terminal. The AISH session is started in
the right pane of the terminal window. The left pane is the current shell session. You must use
these windows to interact with the AISH session.

## EXAMPLES

### Example 1 - Start an AISH session

```powershell
Start-Aish
```

The cmdlet looks for the`aish` executable in the locations listed in the `$env:PATH` environment variable.

### Example 2 - Start an AISH session with a specific path

```powershell
Start-AISH -PATH C:\Users\aish.exe
```

## PARAMETERS

### -Path

By default, the cmdlet looks for the`aish` executable in the locations listed in the `$env:PATH`
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

[Invoke-Aish](Invoke-Aish.md)

[Resolve-Error](Resolve-Error.md)
