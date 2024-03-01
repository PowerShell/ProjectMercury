## Copyright (c) Microsoft Corporation.
## Licensed under the MIT License.

#Requires -Version 7.2

[CmdletBinding()]
param (
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = "Debug",

    [Parameter()]
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string] $Runtime = [NullString]::Value,

    [Parameter()]
    [ValidateSet('openai-gpt', 'az-agent')]
    [string[]] $AgentToInclude,

    [Parameter()]
    [switch] $Clean
)

function GetProjectFile($dir)
{
    return Get-Item "$dir/*.csproj" | ForEach-Object FullName
}

$ErrorActionPreference = 'Stop'

$AgentToInclude ??= @('openai-gpt', 'az-agent')
$RID = $Runtime ?? (dotnet --info |
    Select-String '^\s*RID:\s+(\w+-\w+)$' |
    Select-Object -First 1 |
    ForEach-Object { $_.Matches.Groups[1].Value })
Write-Verbose "RID: $RID"

$shell_dir = Join-Path $PSScriptRoot "shell"
$app_dir = Join-Path $shell_dir "ShellCopilot.App"
$open_ai_agent_dir = Join-Path $shell_dir "ShellCopilot.OpenAI.Agent"
$az_agent_dir = Join-Path $shell_dir "ShellCopilot.Azure.Agent"

$pkg_out_dir = Join-Path $PSScriptRoot "out" "package"
$app_out_dir = Join-Path $PSScriptRoot "out" $Configuration.ToLower()
$open_ai_out_dir = Join-Path $app_out_dir "agents" "ShellCopilot.OpenAI.Agent"
$az_out_dir = Join-Path $app_out_dir "agents" "ShellCopilot.Azure.Agent"

if ($Clean) {
    $out_path = Join-Path $PSScriptRoot "out"
    if (Test-Path $out_path) {
        Write-Verbose "Deleting $out_path" -Verbose
        Remove-Item -Recurse -Force -Path $out_path
    }
}

## Create the package folder. Build will fail when nuget.config references to non-existing path.
if (-not (Test-Path $pkg_out_dir)) {
    New-Item $pkg_out_dir -ItemType Directory > $null
}

Write-Host "`n[Build Shell Copilot ...]`n" -ForegroundColor Green
$app_csproj = GetProjectFile $app_dir
dotnet publish $app_csproj -c $Configuration -o $app_out_dir -r $RID --sc

if ($LASTEXITCODE -eq 0) {
    ## Move the nuget package to the package folder.
    Move-Item $app_out_dir/ShellCopilot.Abstraction.*.nupkg $pkg_out_dir -Force

    if ($AgentToInclude -contains 'openai-gpt') {
        Write-Host "`n[Build the OpenAI agent ...]`n" -ForegroundColor Green
        $open_ai_csproj = GetProjectFile $open_ai_agent_dir
        dotnet publish $open_ai_csproj -c $Configuration -o $open_ai_out_dir
    }
}

if ($LASTEXITCODE -eq 0 -and $AgentToInclude -contains 'az-agent') {
    Write-Host "`n[Build the Azure agents ...]`n" -ForegroundColor Green
    $az_csproj = GetProjectFile $az_agent_dir
    dotnet publish $az_csproj -c $Configuration -o $az_out_dir
}

if ($LASTEXITCODE -eq 0) {
    $shell_path = Join-Path $app_out_dir ($IsWindows ? "aish.exe" : "aish")
    Set-Clipboard $shell_path
    Write-Host "`nBuild was successful, output path: $shell_path " -NoNewline -ForegroundColor Green
    Write-Host "(copied to clipboard)`n" -ForegroundColor Cyan
}
