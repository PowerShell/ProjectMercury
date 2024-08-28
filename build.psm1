## Copyright (c) Microsoft Corporation.
## Licensed under the MIT License.

#Requires -Version 7.2

$metadata = Get-Content $PSScriptRoot/tools/Metadata.json | ConvertFrom-Json
$dotnetSDKVersion = $(Get-Content $PSScriptRoot/global.json | ConvertFrom-Json).Sdk.Version
$dotnetLocalDir = if ($IsWindows) { "$env:LocalAppData\Microsoft\dotnet" } else { "$env:HOME/.dotnet" }

function Start-Build
{
    [CmdletBinding()]
    param (
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration = "Debug",

        [Parameter()]
        [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
        [string] $Runtime = [NullString]::Value,

        [Parameter()]
        [ValidateSet('openai-gpt', 'interpreter', 'ollama')]
        [string[]] $AgentToInclude,

        [Parameter()]
        [switch] $Clean,

        [Parameter()]
        [switch] $PassThru
    )

    $ErrorActionPreference = 'Stop'

    if (-not $AgentToInclude) {
        $agents = $metadata.AgentsToInclude
        $AgentToInclude = if ($agents -eq "*") {
            @('openai-gpt', 'interpreter', 'ollama')
        } else {
            $agents.Split(",", [System.StringSplitOptions]::TrimEntries)
            Write-Verbose "Include agents specified in Metadata.json"
        }
    }

    $RID = $Runtime ?? (dotnet --info |
        Select-String '^\s*RID:\s+(\w+-\w+)$' |
        Select-Object -First 1 |
        ForEach-Object { $_.Matches.Groups[1].Value })

    Write-Verbose "Runtime: $RID `nAgents: $($AgentToInclude -join ",")"

    $shell_dir = Join-Path $PSScriptRoot "shell"
    $agent_dir = Join-Path $shell_dir "agents"

    $app_dir = Join-Path $shell_dir "AIShell.App"
    $pkg_dir = Join-Path $shell_dir "AIShell.Abstraction"
    $module_dir = Join-Path $shell_dir "AIShell.Integration"

    $openai_agent_dir = Join-Path $agent_dir "AIShell.OpenAI.Agent"
    $interpreter_agent_dir = Join-Path $agent_dir "AIShell.Interpreter.Agent"
    $ollama_agent_dir = Join-Path $agent_dir "AIShell.Ollama.Agent"

    $config = $Configuration.ToLower()
    $out_dir = Join-Path $PSScriptRoot "out"
    $pkg_out_dir = Join-Path $out_dir "package"
    $app_out_dir = Join-Path $out_dir $config "app"
    $module_out_dir = Join-Path $out_dir $config "module" "AIShell"
    $module_help_dir= Join-Path $PSScriptRoot "docs" "cmdlets"

    $openai_out_dir = Join-Path $app_out_dir "agents" "AIShell.OpenAI.Agent"
    $interpreter_out_dir = Join-Path $app_out_dir "agents" "AIShell.Interpreter.Agent"
    $ollama_out_dir =  Join-Path $app_out_dir "agents" "AIShell.Ollama.Agent"

    if ($Clean) {
        if (Test-Path $out_dir) {
            Write-Verbose "Deleting $out_dir" -Verbose
            Remove-Item -Recurse -Force -Path $out_dir
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

        if ($PassThru) {
            return [PSCustomObject]@{
                Out = $out_dir
                App = $app_out_dir
                Module = $module_out_dir
            }
        }
    }
}

function GetProjectFile($dir)
{
    return Get-Item "$dir/*.csproj" | ForEach-Object FullName
}

<#
.SYNOPSIS
    Find the dotnet SDK that meets the minimal version requirement.
#>
function Find-Dotnet
{
    $dotnetFile = $IsWindows ? "dotnet.exe" : "dotnet"
    $dotnetPath = Join-Path -Path $dotnetLocalDir -ChildPath $dotnetFile

    # If dotnet is already in the PATH, check to see if that version of dotnet can find the required SDK.
    # This is "typically" the globally installed dotnet.
    $dotnetInPath = Get-Command 'dotnet' -ErrorAction Ignore
    $foundRequiredSDK = $dotnetInPath ? (Test-DotnetSDK $dotnetInPath.Source) : $false

    if (-not $foundRequiredSDK) {
        if (Test-DotnetSDK $dotnetPath) {
            Write-Warning "Prepending '$dotnetLocalDir' to PATH for the required .NET SDK $dotnetSDKVersion."
            $env:PATH = $dotnetLocalDir + [IO.Path]::PathSeparator + $env:PATH
        } else {
            throw "Cannot find the dotnet SDK with the version $dotnetSDKVersion. Please run 'Install-Dotnet'."
        }
    }
}

<#
.SYNOPSIS
    Check if the dotnet SDK meets the minimal version requirement.
#>
function Test-DotnetSDK
{
    param($dotnetPath)

    if (Test-Path $dotnetPath) {
        $installedVersions = & $dotnetPath --list-sdks | ForEach-Object {
            # this splits strings like
            # '6.0.202 [C:\Program Files\dotnet\sdk]'
            # '7.0.100-preview.2.22153.17 [C:\Users\johndoe\AppData\Local\Microsoft\dotnet\sdk]'
            # into version and path parts.
            ($_ -split '\s',2)[0]
        }

        return $installedVersions -contains $dotnetSDKVersion
    }

    return $false
}

<#
.SYNOPSIS
    Install the dotnet SDK if we cannot find an existing one.
#>
function Install-Dotnet
{
    [CmdletBinding()]
    param(
        [string] $Version = $dotnetSDKVersion
    )

    try {
        Find-Dotnet
        return  # Simply return if we find dotnet SDk with the correct version
    } catch { }

    $logMsg = if (Get-Command 'dotnet' -ErrorAction Ignore) {
        "dotnet SDK out of date. Require '$dotnetSDKVersion' but found '$dotnetSDKVersion'. Updating dotnet."
    } else {
        "dotent SDK is not present. Installing dotnet SDK."
    }

    Write-Log $logMsg -Warning
    $obtainUrl = "https://dotnet.microsoft.com/download/dotnet/scripts/v1"

    try {
        Remove-Item $dotnetLocalDir -Recurse -Force -ErrorAction Ignore
        $installScript = $IsWindows ? "dotnet-install.ps1" : "dotnet-install.sh"
        Invoke-WebRequest -Uri $obtainUrl/$installScript -OutFile $installScript

        if ($IsWindows) {
            & .\$installScript -Version $Version
        } else {
            bash ./$installScript -v $Version
        }
    } finally {
        Remove-Item $installScript -Force -ErrorAction Ignore
    }
}

<#
.SYNOPSIS
    Write log message for the build.
#>
function Write-Log
{
    param(
        [string] $Message,
        [switch] $Warning,
        [switch] $Indent
    )

    $foregroundColor = if ($Warning) { "Yellow" } else { "Green" }
    $indentPrefix = if ($Indent) { "    " } else { "" }
    Write-Host -ForegroundColor $foregroundColor "${indentPrefix}${Message}"
}

<#
.SYNOPSIS
    Set credential/PAT for write permission in release build, so that any
    new NuGet packages can be pulled into the PowerShell public feed.
#>
function Set-NuGetSourceCred
{
    param(
        [Parameter(Mandatory)]
        [string] $UserName,

        [Parameter(Mandatory)]
        [string] $ClearTextPAT
    )

    $nugetPath = "$PSScriptRoot/shell/nuget.config"
    $tempFile = [System.IO.Path]::GetTempFileName()

    Get-Content $nugetPath | Where-Object { $_ -ne "</configuration>" } | Out-File $tempFile -Encoding utf8
    Add-Content $tempFile -Value @"
  <packageSourceCredentials>
    <PowerShell_PublicPackages>
      <add key="Username" value="$UserName" />
      <add key="ClearTextPassword" value="$ClearTextPAT" />
    </PowerShell_PublicPackages>
  </packageSourceCredentials>
</configuration>
"@

    Move-Item -Path $tempFile -Destination $nugetPath -Force
}

<#
.SYNOPSIS
    Bootstrap for building.
#>
function Start-Bootstrap
{
    # Verify if the required version of .NET SDK is available, and install it if not.
    Install-Dotnet
}

<#
.SYNOPSIS
    Copy all the 1st-party files to be signed to the target location, in the same relative folder structure.
#>
function Copy-1PFilesToSign
{
    param(
        [Parameter()]
        [string] $SourceRoot,
        [Parameter()]
        [string] $TargetRoot
    )

    $pattern = "*.ps*1", "AIShell.*.dll", "aish.dll", "aish.exe", "Markdown.VT.dll", "ReadLine.dll"

    if (Test-Path $TargetRoot) {
        Remove-Item -Path $TargetRoot -Recurse -Force
    }
    $null = New-Item -ItemType Directory -Path $TargetRoot -Force

    $SourceRoot = (Resolve-Path $SourceRoot).Path
    $TargetRoot = (Resolve-Path $TargetRoot).Path

    Push-Location $SourceRoot
    $filesToSign = Get-ChildItem $pattern -Recurse

    Write-Verbose "List all files to be signed:" -Verbose
    $filesToSign | Out-String -Width 500 -Stream | Write-Verbose -Verbose

    foreach ($file in $filesToSign) {
        $parent = $file.DirectoryName

        if ($parent -eq $SourceRoot) {
            Copy-Item -Path $file.FullName -Destination $TargetRoot
        } else {
            $targetParent = $parent.Replace($SourceRoot, $TargetRoot)
            $null = mkdir -Path $targetParent
            Copy-Item -Path $file.FullName -Destination $targetParent
        }
    }
    Pop-Location

    Write-Verbose "Copy is done. List all files that were copied:"
    Get-ChildItem $TargetRoot -Recurse | Out-String -Width 500 -Stream | Write-Verbose -Verbose
}

function Copy-3PFilesToSign
{
    param(
        [Parameter()]
        [string] $SourceRoot,
        [Parameter()]
        [string] $TargetRoot
    )

    if (Test-Path $TargetRoot) {
        Remove-Item -Path $TargetRoot -Recurse -Force
    }
    $null = New-Item -ItemType Directory -Path $TargetRoot -Force

    Push-Location $SourceRoot
    $unsigned = Get-ChildItem *.dll, *.exe -Recurse | Where-Object {
        $signature = Get-AuthenticodeSignature -FilePath $_.FullName
        $signature.Status -eq 'notsigned' -or $signature.SignerCertificate.Issuer -notmatch '^CN=Microsoft.*'
    }

    Write-Verbose "List all files to be signed:" -Verbose
    $unsigned | Out-String -Width 500 -Stream | Write-Verbose -Verbose

    foreach ($file in $unsigned) {
        $parent = $file.DirectoryName

        if ($parent -eq $SourceRoot) {
            Copy-Item -Path $file.FullName -Destination $TargetRoot
        } else {
            $targetParent = $parent.Replace($SourceRoot, $TargetRoot)
            $null = mkdir -Path $targetParent
            Copy-Item -Path $file.FullName -Destination $targetParent
        }
    }
    Pop-Location

    Write-Verbose "Copy is done. List all files that were copied:"
    Get-ChildItem $TargetRoot -Recurse | Out-String -Width 500 -Stream | Write-Verbose -Verbose
}

<#
.SYNOPSIS
    Copy all the signed files back to overwrite the original unsigned files.
#>
function Copy-SignedFileBack
{
    param(
        [Parameter()]
        [string] $SourceRoot,
        [Parameter()]
        [string] $TargetRoot
    )

    $SourceRoot = (Resolve-Path $SourceRoot).Path
    $TargetRoot = (Resolve-Path $TargetRoot).Path

    Push-Location $SourceRoot
    $signedFiles = Get-ChildItem -Recurse

    Write-Verbose "List all signed files:" -Verbose
    $signedFiles | Out-String -Width 500 -Stream | Write-Verbose -Verbose

    $dests = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $signedFiles) {
        $parent = $file.DirectoryName
        $leaf = $file.Name

        if ($parent -eq $SourceRoot) {
            $destPath = Join-Path $TargetRoot $leaf
            Copy-Item -Path $file.FullName -Destination $destPath -Force
        } else {
            $targetParent = $parent.Replace($SourceRoot, $TargetRoot)
            $destPath = Join-Path $targetParent $leaf
            Copy-Item -Path $file.FullName -Destination $destPath -Force
        }

        $dests.Add($destPath)
    }

    Write-Verbose "Copy is done. List all copied-to files:" -Verbose
    $dests | Write-Verbose -Verbose
}

Export-ModuleMember -Function Start-Build, Find-Dotnet, Install-Dotnet, Set-NuGetSourceCred, Start-Bootstrap, Copy-1PFilesToSign, Copy-3PFilesToSign, Copy-SignedFileBack
