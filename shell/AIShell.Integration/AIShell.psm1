
## Create the channel singleton when loading the module.
$null = [AIShell.Integration.Channel]::CreateSingleton($host.Runspace, [Microsoft.PowerShell.PSConsoleReadLine])
