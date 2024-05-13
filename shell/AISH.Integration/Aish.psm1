
## Create the channel singleton when loading the module.
$null = [AISH.Integration.AishChannel]::CreateSingleton($host.Runspace, [Microsoft.PowerShell.PSConsoleReadLine])
