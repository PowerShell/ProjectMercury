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
    [Parameter(HelpMessage = "Specify this parameter to uninstall AIShell")]
    [switch] $Uninstall
)

<#################################################
#
#               Helper functions
#
#################################################>

function Get-PowerShellVersion {
    if ([version]$PSVersionTable.PSVersion -lt [version]"7.4.6") {
        Write-Warning "[PowerShell version 7.4.6 is the minimum requirement for AIShell module]`n"
        return ""
    }
    return $True
}

function Get-InstallDirectoryForCurrentOS {
    $destinationDirectoryForMac = Join-Path "usr" "local" "AIShell"
    $destinationDirectoryForWindows = Join-Path $env:LOCALAPPDATA "Programs" "AIShell"
    # check os
    if ($IsWindows) {
        return $destinationDirectoryForWindows
    } 
    elseif ($IsMacOS) {
        return $destinationDirectoryForMac
    }
    else {
        Write-Warning "This install script is only compatible with Windows and Mac systems, if you want to install on Linux please download the package directly from the GitHub repo at aka.ms/AIShell-Repo"
        return ""
    }
}

function Save-AIShellFromUrl {
    # using placeholder urls until the GitHub release files are ready
    $osxArm64Url = "osxArm64Url"
    $osxX64Url = "osxX64Url"
    $winArm64Url = "winArm64Url"
    $winX64Url = "winX64Url"
    $winX86Url = "winX86Url"
    $moduleUrl = "moduleUrl"

    if (!$moduleUrl) {
        return
    }

    # Determine download url based on OS and architecture
    $destinationDirectory = Get-InstallDirectoryForCurrentOS
    $osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture

    $buildUrl = ""
    if ($IsWindows) {
        if ($osArchitecture -eq "X64") {
            $buildUrl = $winX64Url
        } 
        elseif ($osArchitecture -eq "X86") {
            $buildUrl = $winX86Url
        } 
        elseif ($osArchitecture -eq "Arm64") {
            $buildUrl = $winArm64Url
        } 
    } 
    elseif ($IsMacOS) {
        if ($osArchitecture -eq "X64") {
            $buildUrl = $osxX64Url
        } 
        elseif ($osArchitecture -eq "Arm64") {
            $buildUrl = $osxArm64Url
        } 
    }

    # Create the directory if not existed
    if(!(Test-Path -path $destinationDirectory)) {
        New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null
    }

    # Download AIShell Module
    $tempFilesDirectory = Join-Path $destinationDirectory "temp"
    if(!(Test-Path -path $tempFilesDirectory)) {
        New-Item -Path $tempFilesDirectory -ItemType Directory -Force | Out-Null
    }
    Write-Host "[Downloading the latest AIShell PowerShell Module to $tempFilesDirectory ...]`n" -ForegroundColor Green
    Invoke-WebRequest -Uri $moduleUrl -OutFile $tempFilesDirectory

    # Download OS specific AIShell build
    $osPlatform = ""
    $destinationArchiveFile = ""
    if ($IsWindows) {
        $osPlatform = "Windows"
        $destinationArchiveFile = Join-Path $tempFilesDirectory "AIShell.zip"
    } 
    elseif ($IsMacOS) {
        $osPlatform = "MacOS"
        $destinationArchiveFile = Join-Path $tempFilesDirectory "AIShell.tar.gz"
    }
    Write-Host "[Downloading the latest AIShell for $osPlatform $osArchitecture to $destinationArchiveFile ...]`n" -ForegroundColor Green
    Invoke-WebRequest -Uri $buildUrl -OutFile $destinationArchiveFile
    
    # Unzip AIShell
    Write-Host "[Unzipping AIShell to $destinationDirectory ...]" -ForegroundColor Green
    Unblock-File -Path $destinationArchiveFile
    Expand-Archive -Path $destinationArchiveFile -DestinationPath $destinationDirectory -Force
    if ($IsMacOS) {
        $aishPath = Join-Path $destinationArchiveFile "aish"
        chmod +x $aishPath
    }
}

function Remove-AIShellFromLocalDir {
    $DestinationPath = Get-InstallDirectoryForCurrentOS
    if (Test-Path $DestinationPath) {
            Write-Host "[Removing AIShell from $DestinationPath]" -ForegroundColor Green
            Remove-Item -Path $DestinationPath -Recurse -Force
            Write-Host "[AIShell removed from $DestinationPath]" -ForegroundColor Green
    } 
    else {
        Write-Host "[AIShell cannot be found at $DestinationPath, skip removing]" -ForegroundColor Yellow
    }
}

function Register-AIShellModule {
    Write-Host "[Installing AIShell Module]" -ForegroundColor Green
    $aishModulePath = Join-Path $(Get-InstallDirectoryForCurrentOS) "temp"
    $repoName = "tempRepositoryForAIShell"
    Register-PSRepository -Name $repoName -SourceLocation $aishModulePath -InstallationPolicy Trusted
    Install-Module -Name "AIShell" -Repository $repoName -AllowClobber
    Unregister-PSRepository -Name $repoName
    Remove-Item -Path $aishModulePath -Recurse -Force
    Write-Host "[AIShell Module installed]" -ForegroundColor Green
}

function Unregister-AIShellModule {
    if (Get-InstalledModule -Name "AIShell") {
        Write-Host "[Uninstalling AIShell Module]" -ForegroundColor Green
        Uninstall-Module -Name "AIShell" -Force
        Write-Host "[AIShell Module uninstalled]" -ForegroundColor Green
    } 
    else {
        Write-Host "[AIShell Module cannot be found, skip uninstalling]" -ForegroundColor Yellow
    }
}

function Test-AIShellExistInPath {
    $aishPath =  Get-InstallDirectoryForCurrentOS
    return ($env:PATH -like "*$aishPath*")
}

function Add-AIShellToPath {
    if (Test-AIShellExistInPath) {
        Write-Host "[AIShell was already added in PATH]" -ForegroundColor Green
    } else {
        Write-Host "[Adding AIShell to PATH]" -ForegroundColor Green
        $processPath = $env:PATH
        if (!$processPath.EndsWith("$")) {
            $processPath += ";"
        }
        $aishPath =  Get-InstallDirectoryForCurrentOS
        [Environment]::SetEnvironmentVariable("PATH", $processPath + $aishPath, "Process")
        Write-Host "[AIShell added to PATH]" -ForegroundColor Green
    }
}

function Remove-AIShellFromPath {
    if (Test-AIShellExistInPath) {
        Write-Host "[Removing AIShell from PATH]" -ForegroundColor Green
        $aishPath =  Get-InstallDirectoryForCurrentOS
        $processPath = $env:PATH
        $processPath = ($processPath.Split(';') | Where-Object { $_ -ne $AishPath }) -join ';'
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

if (!Get-PowerShellVersion) {
    return
}

if (!Get-InstallDirectoryForCurrentOS) {
    return 
}

Write-Host "----------------------------------------`n" -ForegroundColor Green

if ($Uninstall) {
    Unregister-AIShellModule
} 
else {
    Save-AIShellFromUrl
}

Write-Host "`n----------------------------------------`n" -ForegroundColor Green

if ($Uninstall) {
    Remove-AIShellFromLocalDir  
} 
else {
    Register-AIShellModule
}

Write-Host "`n----------------------------------------`n" -ForegroundColor Green

if ($Uninstall) {
    Remove-AIShellFromPath
} 
else {
    Add-AIShellToPath
}

Write-Host "`n----------------------------------------`n" -ForegroundColor Green


if ($Uninstall) {
    Write-Host "AIShell has been fully uninstalled." -ForegroundColor Green
} 
else {
    Write-Host "Please run ``Start-AIShell`` now." -ForegroundColor Green
}