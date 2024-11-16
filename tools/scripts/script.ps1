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

$Script:MacSymbolicLink = '/usr/local/bin/aish'
$Script:MacInstallationLocation = "/usr/local/AIShell"
$Script:WinInstallationLocation = "$env:LOCALAPPDATA\Programs\AIShell"
$Script:InstallLocation = $null
$Script:PackageURL = $null

function Resolve-Environment {
    if ($PSVersionTable.PSVersion -lt [version]"7.4.6") {
        throw "PowerShell v7.4.6 is required for using the AIShell module."
    }
    if ($IsLinux) {
        throw "Sorry, this install script is only compatible with Windows and macOS. If you want to install on Linux, please download the package directly from the GitHub repo at aka.ms/AIShell-Repo."
    }

    ($platShortName, $platFullName, $pkgExt, $location) = if ($IsWindows) {
        'win', 'Windows', 'zip', $Script:WinInstallationLocation
    } else {
        'osx', 'macOS', 'tar.gz', $Script:MacInstallationLocation
    }

    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    if ($architecture -notin @('X86', 'X64', 'Arm64')) {
        throw "AI Shell doesn't support the $architecture architecture on $platFullName."
    }

    $Script:InstallLocation = $location
    $Script:PackageURL = "https://github.com/PowerShell/ProjectMercury/releases/download/v1.0.0-preview.1/AIShell-1.0.0-preview.1-${platShortName}-$($architecture.ToLower()).${pkgExt}"
}

function Install-AIShellApp {
    $destination = $Script:InstallLocation
    $packageUrl = $Script:PackageURL

    if (Test-Path -path $destination) {
        # TODO: need to check if the folder is empty, and if not, prompt to ask if we should delete all from within the folder.
        # skip installing AIShell app if user respond 'No' for deleting.
    } else {
        # Create the directory if not existing.
        if ($IsWindows) {
            $null = New-Item -Path $destination -ItemType Directory -Force
        } else {
            # '/usr/local' requires sudo.
            sudo mkdir $destination
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to create the installation folder '$destination'."
            }
        }
    }

    $fileName = [System.IO.Path]::GetFileName($packageUrl)
    $tempPath = Join-Path $env:TEMP $fileName
    if (Test-Path $tempPath) {
        Remove-Item $tempPath -Force -ErrorAction Stop
    }

    # Download AIShell package.
    Write-Host "[Downloading AIShell package '$fileName' ...]`n" -ForegroundColor Green
    Invoke-WebRequest -Uri $packageUrl -OutFile $tempPath -ProgressAction Ignore -ErrorAction Stop

    # Extract AIShell package.
    Write-Host "[Extracting AIShell to '$destination' ...]" -ForegroundColor Green
    Unblock-File -Path $tempPath
    if ($IsWindows) {
        Expand-Archive -Path $tempPath -DestinationPath $destination -Force -ErrorAction Stop

        # Set the process-scope and user-scope Path env variables to include AIShell.
        $envPath = $env:Path
        if (-not $envPath.Contains($destination)) {
            Write-Host "[Adding AIShell to Path]" -ForegroundColor Green
            $env:Path = "${destination};${envPath}"
            $userPath = [Environment]::GetEnvironmentVariable('Path', [EnvironmentVariableTarget]::User)
            $newUserPath = $userPath.EndsWith(';') ? "${userPath}${destination}" : "${userPath};${destination}"
            [Environment]::SetEnvironmentVariable('Path', $newUserPath, [EnvironmentVariableTarget]::User)
            Write-Host "[AIShell added to Path]" -ForegroundColor Green
        }
    } else {
        sudo tar -xzf yourfile.tar.gz -C $destination
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to extract '$tempPath' to the folder '$destination'."
        }

        $aishPath = Join-Path $destination 'aish'
        sudo chmod +x $aishPath
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to set the execution permission to the executable '$aishPath'."
        }

        # No need to setup the Path env variable as the symbolic link is already within Path.
        $symlink = $Script:MacSymbolicLink
        sudo ln -s $aishPath $symlink
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create the symbolic link '$symlink' to '$aishPath'."
        }
    }
}

function Uninstall-AIShellApp {
    $destination = $Script:InstallLocation
    if (Test-Path $destination) {
        if ($IsWindows) {
            Remove-Item -Path $destination -Recurse -Force -ErrorAction Stop

            # Update the user-scope Path env variables to remove AIShell.
            $userPath = [Environment]::GetEnvironmentVariable('Path', [EnvironmentVariableTarget]::User)
            if ($userPath.Contains($destination)) {
                Write-Host "[Removing AIShell from user-scope Path]" -ForegroundColor Green
                $newUserPath = $userPath.Split(';', [StringSplitOptions]::RemoveEmptyEntries -bor [StringSplitOptions]::TrimEntries) |
                    Where-Object { $_ -ne $destination } |
                    Join-String -Separator ';'
                [Environment]::SetEnvironmentVariable("Path", $newUserPath, [EnvironmentVariableTarget]::User)
                Write-Host "[AIShell removed from user-scope PATH]" -ForegroundColor Green
            }
        } else {
            sudo rm -rf $destination
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to remove the AIShell app from '$destination'."
            }

            $symlink = $Script:MacSymbolicLink
            sudo rm $symlink
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to remove the symbolic link '$symlink'."
            }
        }
        Write-Host "[AIShell removed from '$destination']" -ForegroundColor Green
    } else {
        Write-Host "[AIShell cannot be found at '$destination', skip removing]" -ForegroundColor Yellow
    }
}

function Install-AIShellModule {
    Write-Host "[Installing the PowerShell module 'AIShell' ...]`n" -ForegroundColor Green
    Install-PSResource -Name AIShell -Repository PSGallery -Prerelease -ErrorAction Stop
    Write-Host "[AIShell Module installed]" -ForegroundColor Green
}

function Uninstall-AIShellModule {
    if (Get-InstalledPSResource -Name "AIShell") {
        try {
            Write-Host "[Uninstalling AIShell Module ...]" -ForegroundColor Green
            Uninstall-PSResource -Name AIShell -ErrorAction Stop
            Write-Host "[AIShell Module uninstalled]" -ForegroundColor Green
        } catch {
            throw "Failed to uninstall the 'AIShell' module. Please check if the module got imported in any active PowerShell session. If so, please exit the session and try this script again."
        }
    } else {
        Write-Host "[AIShell Module cannot be found, skip uninstalling]" -ForegroundColor Yellow
    }
}

<###################################
#
#           Setup/Execute
#
###################################>

Resolve-Environment

if ($Uninstall) {
    Uninstall-AIShellApp
} else {
    Install-AIShellApp
}

Write-Host "`n----------------------------------------`n" -ForegroundColor Green

if ($Uninstall) {
    Uninstall-AIShellModule
} else {
    Install-AIShellModule
}

Write-Host "`n----------------------------------------`n" -ForegroundColor Green

if ($Uninstall) {
    Write-Host "AIShell has been fully uninstalled." -ForegroundColor Green
} else {
    Write-Host "Installation succeeded. Please run ``Start-AIShell``." -ForegroundColor Green
}
