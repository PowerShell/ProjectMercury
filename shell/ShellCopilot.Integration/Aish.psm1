
## Create the channel singleton when loading the module.
$null = [ShellCopilot.Integration.AishChannel]::CreateSingleton($host.Runspace, [Microsoft.PowerShell.PSConsoleReadLine])
