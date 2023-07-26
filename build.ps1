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

try {
    Push-Location "$PSScriptRoot/src/code"

    $outPath = "$PSScriptRoot/out/ai"

    if ($Clean) {
        if (Test-Path $outPath) {
            Write-Verbose "Deleting $outPath"
            Remove-Item -recurse -force -path $outPath
        }

        dotnet clean
    }

    dotnet publish --output $outPath --configuration $Configuration
}
finally {
    Pop-Location
}
