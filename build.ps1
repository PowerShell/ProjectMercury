## Copyright (c) Microsoft Corporation.
## Licensed under the MIT License.

[CmdletBinding()]
param (
    [Parameter()]
    [string]
    $Configuration = "Debug",

    [Parameter()]
    [switch]
    $Clean,

    [Parameter()]
    [switch]
    $ShellCopilot,

    [Parameter()]
    [switch]
    $PSCopilot
)

try {

    if($ShellCopilot) {
        Write-Host "Building ShellCopilot..."
        Push-Location "$PSScriptRoot/shell/ShellCopilot.App"

        $outPath = "$PSScriptRoot/out/ShellCopilot.App"

        if ($Clean) {
            if (Test-Path $outPath) {
                Write-Verbose "Deleting $outPath"
                Remove-Item -recurse -force -path $outPath
            }

            dotnet clean
        }

        dotnet publish --output $outPath --configuration $Configuration
        Write-Host "ShellCopilot built successfully, output path: $outPath"
    }

    elseif($PSCopilot) {
        Write-Host "Building PSCopilot..."
        Push-Location "$PSScriptRoot/src/code"

        $outPath = "$PSScriptRoot/out/Microsoft.PowerShell.Copilot"

        if ($Clean) {
            if (Test-Path $outPath) {
                Write-Verbose "Deleting $outPath"
                Remove-Item -recurse -force -path $outPath
            }

            dotnet clean
        }

        dotnet publish --output $outPath --configuration $Configuration
        Write-Host "ShellCopilot built successfully, output path: $outPath"
    }
    else {
        Write-Host "Please specify which prototype to build with -ShellCopilot or -PSCopilot switch paramters"
    }
    
}
finally {
    Pop-Location
}
