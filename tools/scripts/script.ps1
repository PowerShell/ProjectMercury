# ----------------------------------------------------------------------------------
#
# Copyright Microsoft Corporation
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
# http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
# ----------------------------------------------------------------------------------

param(
    [Parameter(Mandatory = $false, Position = 0, HelpMessage = "To uninstall. By default false.")]
    [switch]$uninstall
)

<#################################################
#
#               Helper functions
#
#################################################>

function Get-PowerShellVersion{
    if ($PSVersionTable.PSVersion.Major -lt 7){
        Write-Warning "[For the best experience please install PowerShell version 7.]`n"
    }
}

function Save-AIShellFromUrl{
    # using placeholder urls until the GitHub release files are ready
    $osxArm64Url = "osxArm64Url"
    $osxX64Url = "osxX64Url"
    $winArm64Url = "winArm64Url"
    $winX64Url = "winX64Url"
    $moduleUrl = "moduleUrl"

    if (!$moduleUrl) {
        return
    }
    
    $destinationDirectory = (Join-Path (Join-Path (Join-Path $env:userprofile ".azure") "bin") "aishell")

    # Create the directory if not existed
    if(!(Test-Path -path $destinationDirectory))  {
        New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null
    }

    # Download AIShell Module
    $destinationZipFile = Join-Path $destinationDirectory "AIShell-Module.zip"
    Write-Host "[Downloading the latest AIShell PowerShell Module to $destinationZipFile ...]`n" -ForegroundColor Green
    # Fix for where downloading with Invoke-WebRequest is too slow
    # $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $moduleUrl -OutFile $destinationZipFile
    
    # Unzip AIShell Module
    $destinationUnzippedFolder = Join-Path $destinationDirectory "AIShell-Module"
    Write-Host "[Unzipping the module to $destinationUnzippedFolder ...]`n" -ForegroundColor Green
    # adding -ErrorAction SilentlyContinue because if user is not using Powershell as an administrator, the AIShell module dll files would not be able to be deleted until the current session has been closed. 
    Expand-Archive -Path $destinationZipFile -DestinationPath $destinationUnzippedFolder -Force -ErrorAction SilentlyContinue

    # check os
    $osPlatform = ""
    if ($IsWindows) {
        $osPlatform = "Windows"
    } 
    elseif ($IsMacOS) {
        $osPlatform = "MacOS"
    }
    else {
        Write-Error "Sorry, AIShell currently only supports Windows and MacOS"
        exit 1
    }

    # check architecture
    $osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    if ($osArchitecture -ne "X64" -and $osArchitecture -ne "Arm64") {
        Write-Error "Sorry, AIShell currently only supports x64 and arm64 architectures"
        exit 1
    }

    # Download OS specific AIShell build
    $downloadUrl = ""
    if ($osPlatform -eq "Windows" -and $osArchitecture -eq "X64"){
        $downloadUrl = $winX64Url
    } 
    elseif ($osPlatform -eq "Windows" -and $osArchitecture -eq "Arm64"){
        $downloadUrl = $winArm64Url
    } 
    elseif ($osPlatform -eq "MacOS" -and $osArchitecture -eq "X64"){
        $downloadUrl = $osxX64Url
    } 
    elseif ($osPlatform -eq "MacOS" -and $osArchitecture -eq "Arm64"){
        $downloadUrl = $osxArm64Url
    } 
    $destinationZipFile = Join-Path $destinationDirectory "AIShell.zip"
    Write-Host "[Downloading the latest AIShell for $osPlatform $osArchitecture to $destinationZipFile ...]`n" -ForegroundColor Green
    Invoke-WebRequest -Uri $downloadUrl -OutFile $destinationZipFile
    # $ProgressPreference = 'Continue'
    
    # Unzip AIShell
    $destinationUnzippedFolder = Join-Path $destinationDirectory "AIShell"
    Write-Host "[Unzipping AIShell to $destinationUnzippedFolder ...]" -ForegroundColor Green
    Expand-Archive -Path $destinationZipFile -DestinationPath $destinationUnzippedFolder -Force
}

function Remove-AIShellFromLocalDir{
    $DestinationPath = (Join-Path (Join-Path (Join-Path $env:userprofile ".azure") "bin") "aishell")
    if (Test-Path $DestinationPath)
    {
            Write-Host "[Removing AIShell from $DestinationPath]" -ForegroundColor Green
            # adding -ErrorAction SilentlyContinue because if user is not using Powershell as an administrator, the AIShell module dll files would not be able to be deleted until the current session has been closed. 
            Remove-Item -Path $DestinationPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "[AIShell removed from $DestinationPath]" -ForegroundColor Green
    } 
    else 
    {
        Write-Host "[AIShell cannot be found at $DestinationPath, skip removing]" -ForegroundColor Yellow
    }
}

function Register-AIShellModule{
    Write-Host "[Installing AIShell Module]" -ForegroundColor Green
    $aishModulePath =  Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $env:userprofile ".azure") "bin") "aishell") "AIShell-Module") AIShell
    Import-Module $aishModulePath
    Write-Host "[AIShell Module installed]" -ForegroundColor Green
}

function Unregister-AIShellModule{
    if (Get-Module -Name "AIShell"){
        Write-Host "[Uninstalling AIShell Module]" -ForegroundColor Green
        Remove-Module -Name "AIShell"
        Write-Host "[AIShell Module uninstalled]" -ForegroundColor Green
    } else {
        Write-Host "[AIShell Module cannot be found, skip uninstalling]" -ForegroundColor Yellow
    }
}

function Test-AIShellExistInPath{
    $aishPath =  Join-Path (Join-Path (Join-Path (Join-Path $env:userprofile ".azure") "bin") "aishell") "AIShell"
    return ([Environment]::GetEnvironmentVariable("PATH", "Process") -like "*$aishPath*") -or ([Environment]::GetEnvironmentVariable("PATH", "User") -like "*$aishPath*")
}

function Add-AIShellToPath{
    if (Test-AIShellExistInPath)
    {
        Write-Host "[AIShell was already added in PATH]" -ForegroundColor Green
    }
    else {
        Write-Host "[Adding AIShell to PATH]" -ForegroundColor Green
        $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
        $processPath = [Environment]::GetEnvironmentVariable("PATH", "Process")
        if ($userPath -notmatch ";$"){
            $userPath += ";"
        }
        if ($processPath -notmatch ";$"){
            $processPath += ";"
        }
        $aishPath =  Join-Path (Join-Path (Join-Path (Join-Path $env:userprofile ".azure") "bin") "aishell") "AIShell"
        [Environment]::SetEnvironmentVariable("PATH", $userPath + $aishPath, "User")
        [Environment]::SetEnvironmentVariable("PATH", $processPath + $aishPath, "Process")
        Write-Host "[AIShell added to PATH]" -ForegroundColor Green
    }
}

function Remove-AIShellFromPath{
    if (Test-AIShellExistInPath)
    {
        Write-Host "[Removing AIShell from PATH]" -ForegroundColor Green
        $aishPath =  Join-Path (Join-Path (Join-Path (Join-Path $env:userprofile ".azure") "bin") "aishell") "AIShell"
        $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
        $processPath = [Environment]::GetEnvironmentVariable("PATH", "Process")
        $userPath = ($userPath.Split(';') | Where-Object { $_ -ne $AishPath }) -join ';'
        $processPath = ($processPath.Split(';') | Where-Object { $_ -ne $AishPath }) -join ';'
        [Environment]::SetEnvironmentVariable("PATH", $userPath, "User")
        [Environment]::SetEnvironmentVariable("PATH", $processPath, "Process")
        Write-Host "[AIShell removed from PATH]" -ForegroundColor Green
    }
    else {
        Write-Host "[AIShell cannot be found in PATH, skip removing]" -ForegroundColor Yellow
    }
}

<###################################
#
#           Setup/Execute
#
###################################>

Get-PowerShellVersion

Write-Host "----------------------------------------`n" -ForegroundColor Green

if ($uninstall){
    Unregister-AIShellModule
} 
else {
    Save-AIShellFromUrl
}

Write-Host "`n----------------------------------------`n" -ForegroundColor Green

if ($uninstall){
    Remove-AIShellFromLocalDir  
} 
else {
    Register-AIShellModule
    
}

Write-Host "`n----------------------------------------`n" -ForegroundColor Green

if ($uninstall){
    Remove-AIShellFromPath
} 
else {
    Add-AIShellToPath
}

Write-Host "`n----------------------------------------`n" -ForegroundColor Green


if ($uninstall){
    Write-Host "AIShell has been fully uninstalled." -ForegroundColor Green
} 
else {
    Write-Host "Please run ``Start-AIShell`` now." -ForegroundColor Green
}