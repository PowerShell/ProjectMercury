param($configuration = "Debug")

function GetProjectFile($dir)
{
    return Get-Item "$dir/*.csproj" | ForEach-Object FullName
}

$app_dir = Join-Path $PSScriptRoot "ShellCopilot.App"
$open_ai_agent_dir = Join-Path $PSScriptRoot "ShellCopilot.OpenAI.Agent"
$az_cli_agent_dir = Join-Path $PSScriptRoot "ShellCopilot.AzCLI.Agent"

$app_out_dir = Join-Path $PSScriptRoot "out"
$open_ai_out_dir = Join-Path $app_out_dir "agents" "ShellCopilot.OpenAI.Agent"
$az_cli_out_dir = Join-Path $app_out_dir "agents" "ShellCopilot.AzCLI.Agent"

if (Test-Path $open_ai_out_dir) {
    Remove-Item $open_ai_out_dir -Recurse -Force
}

if (Test-Path $az_cli_out_dir) {
    Remove-Item $az_cli_out_dir -Recurse -Force
}

Write-Host "`n[Build Shell Copilot ...]`n" -ForegroundColor Green
$app_csproj = GetProjectFile $app_dir
dotnet build $app_csproj -c $configuration -o $app_out_dir

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[Build the OpenAI agent ...]`n" -ForegroundColor Green
    $open_ai_csproj = GetProjectFile $open_ai_agent_dir
    dotnet publish $open_ai_csproj -c $configuration -o $open_ai_out_dir
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[Build the AzCLI agent ...]`n" -ForegroundColor Green
    $az_cli_csproj = GetProjectFile $az_cli_agent_dir
    dotnet publish $az_cli_csproj -c $configuration -o $az_cli_out_dir
}

if ($LASTEXITCODE -eq 0) {
    $shell_path = Join-Path $app_out_dir ($IsWindows ? "aish.exe" : "aish")
    Write-Host "`nShell Copilot: $shell_path`n" -ForegroundColor Green
}
