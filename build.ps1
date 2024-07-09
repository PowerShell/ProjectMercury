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
    [ValidateSet('openai-gpt', 'az-agent', 'interpreter', 'ollama')]
    [string[]] $AgentToInclude,

    [Parameter()]
    [switch] $Clean
)

function GetProjectFile($dir)
{
    return Get-Item "$dir/*.csproj" | ForEach-Object FullName
}

$ErrorActionPreference = 'Stop'

$AgentToInclude ??= @('openai-gpt', 'az-agent', 'interpreter', 'ollama')
$RID = $Runtime ?? (dotnet --info |
    Select-String '^\s*RID:\s+(\w+-\w+)$' |
    Select-Object -First 1 |
    ForEach-Object { $_.Matches.Groups[1].Value })
Write-Verbose "RID: $RID"

$shell_dir = Join-Path $PSScriptRoot "shell"
$agent_dir = Join-Path $shell_dir "agents"

$app_dir = Join-Path $shell_dir "AIShell.App"
$pkg_dir = Join-Path $shell_dir "AIShell.Abstraction"
$module_dir = Join-Path $shell_dir "AIShell.Integration"

$openai_agent_dir = Join-Path $agent_dir "AIShell.OpenAI.Agent"
$az_agent_dir = Join-Path $agent_dir "AIShell.Azure.Agent"
$interpreter_agent_dir = Join-Path $agent_dir "AIShell.Interpreter.Agent"
$ollama_agent_dir = Join-Path $agent_dir "AIShell.Ollama.Agent"

$config = $Configuration.ToLower()
$pkg_out_dir = Join-Path $PSScriptRoot "out" "package"
$app_out_dir = Join-Path $PSScriptRoot "out" $config "app"
$module_out_dir = Join-Path $PSScriptRoot "out" $config "module" "AIShell"
$module_help_dir= Join-Path $PSScriptRoot "docs" "cmdlets"

$openai_out_dir = Join-Path $app_out_dir "agents" "AIShell.OpenAI.Agent"
$az_out_dir = Join-Path $app_out_dir "agents" "AIShell.Azure.Agent"
$interpreter_out_dir = Join-Path $app_out_dir "agents" "AIShell.Interpreter.Agent"
$ollama_out_dir =  Join-Path $app_out_dir "agents" "AIShell.Ollama.Agent"

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

Write-Host "`n[Build AI Shell ...]`n" -ForegroundColor Green
$app_csproj = GetProjectFile $app_dir
dotnet publish $app_csproj -c $Configuration -o $app_out_dir -r $RID --sc

if ($LASTEXITCODE -eq 0) {
    ## Move the nuget package to the package folder.
    Write-Host "`n[Deploy the NuGet package ...]`n" -ForegroundColor Green
    $pkg_csproj = GetProjectFile $pkg_dir
    dotnet pack $pkg_csproj -c $Configuration --no-build -o $pkg_out_dir
}

if ($LASTEXITCODE -eq 0 -and $AgentToInclude -contains 'openai-gpt') {
    Write-Host "`n[Build the OpenAI agent ...]`n" -ForegroundColor Green
    $openai_csproj = GetProjectFile $openai_agent_dir
    dotnet publish $openai_csproj -c $Configuration -o $openai_out_dir
}

if ($LASTEXITCODE -eq 0 -and $AgentToInclude -contains 'az-agent') {
    Write-Host "`n[Build the Azure agents ...]`n" -ForegroundColor Green
    $az_csproj = GetProjectFile $az_agent_dir
    dotnet publish $az_csproj -c $Configuration -o $az_out_dir
}

if ($LASTEXITCODE -eq 0 -and $AgentToInclude -contains 'interpreter') {
    Write-Host "`n[Build the Interpreter agent ...]`n" -ForegroundColor Green
    $interpreter_csproj = GetProjectFile $interpreter_agent_dir
    dotnet publish $interpreter_csproj -c $Configuration -o $interpreter_out_dir
}

if ($LASTEXITCODE -eq 0 -and $AgentToInclude -contains 'ollama') {
    Write-Host "`n[Build the Ollama agent ...]`n" -ForegroundColor Green
    $ollama_csproj = GetProjectFile $ollama_agent_dir
    dotnet publish $ollama_csproj -c $Configuration -o $ollama_out_dir
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[Build the AIShell module ...]`n" -ForegroundColor Green
    $aish_module_csproj = GetProjectFile $module_dir
    dotnet publish $aish_module_csproj -c $Configuration -o $module_out_dir
    
    $installHelp = $false
    if (Get-Module -Name PlatyPS -ListAvailable) {
        $installHelp = $true
    } else {
        Write-Host "`n  The 'PlatyPS' module is not installed. Installing for creating in-shell help ..." -ForegroundColor Green
        Install-Module -Name platyPS -RequiredVersion 0.14.2 -Repository PSGallery -Force
        if ($?) {
            $installHelp = $true
        } else {
            Write-Host "`n  Failed to install the 'PlatyPS' module. In-shell help for the 'AIShell' module will not be created." -ForegroundColor Red
        }
    }

    if ($installHelp) {
        $null = New-ExternalHelp -Path $module_help_dir -OutputPath $module_out_dir -Force
        Write-Host "  In-shell help for the 'AIShell' module has been created." -ForegroundColor Green
    }
}

if ($LASTEXITCODE -eq 0) {
    $shell_path = Join-Path $app_out_dir ($IsWindows ? "aish.exe" : "aish")
    Set-Clipboard $shell_path
    Write-Host "`nBuild was successful, output path: $shell_path " -NoNewline -ForegroundColor Green
    Write-Host "(copied to clipboard)`n" -ForegroundColor Cyan
}
