## Copyright (c) Microsoft Corporation.
## Licensed under the MIT License.

[CmdletBinding()]
param (
    [Parameter()]
    [string]
    $Configuration = "Debug",

    [Parameter()]
    [switch]
    $Clean
)

function GetProjectFile($dir)
{
    return Get-Item "$dir/*.csproj" | ForEach-Object FullName
}

$shell_dir = Join-Path $PSScriptRoot "shell"
$app_dir = Join-Path $shell_dir "ShellCopilot.App"
$open_ai_agent_dir = Join-Path $shell_dir "ShellCopilot.OpenAI.Agent"
$az_cli_agent_dir = Join-Path $shell_dir "ShellCopilot.AzCLI.Agent"

$pkg_out_dir = Join-Path $PSScriptRoot "out" "package"
$app_out_dir = Join-Path $PSScriptRoot "out" $Configuration.ToLower()
$open_ai_out_dir = Join-Path $app_out_dir "agents" "ShellCopilot.OpenAI.Agent"
$az_cli_out_dir = Join-Path $app_out_dir "agents" "ShellCopilot.AzCLI.Agent"

if ($Clean) {
    $out_path = Join-Path $PSScriptRoot "out"
    if (Test-Path $out_path) {
        Write-Verbose "Deleting $out_path" -Verbose
        Remove-Item -Recurse -Force -Path $out_path
    }
}

## Create the package folder. Build will fail when nuget.config references to non-existing path.
if (-not (Test-Path $pkg_out_dir)) {
    mkdir $pkg_out_dir > $null
}

Write-Host "`n[Build Shell Copilot ...]`n" -ForegroundColor Green
$app_csproj = GetProjectFile $app_dir
dotnet build $app_csproj -c $Configuration -o $app_out_dir

if ($LASTEXITCODE -eq 0) {
    ## Move the nuget package to the package folder.
    Move-Item $app_out_dir/ShellCopilot.Abstraction.*.nupkg $pkg_out_dir -Force

    Write-Host "`n[Build the OpenAI agent ...]`n" -ForegroundColor Green
    $open_ai_csproj = GetProjectFile $open_ai_agent_dir
    dotnet publish $open_ai_csproj -c $Configuration -o $open_ai_out_dir
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[Build the AzCLI agent ...]`n" -ForegroundColor Green
    $az_cli_csproj = GetProjectFile $az_cli_agent_dir
    dotnet publish $az_cli_csproj -c $Configuration -o $az_cli_out_dir
}

if ($LASTEXITCODE -eq 0) {
    $shell_path = Join-Path $app_out_dir ($IsWindows ? "aish.exe" : "aish")
    Set-Clipboard $shell_path
    Write-Host "`nBuild was successful, output path: $shell_path " -NoNewline -ForegroundColor Green
    Write-Host "(copied to clipboard)`n" -ForegroundColor Cyan
}
