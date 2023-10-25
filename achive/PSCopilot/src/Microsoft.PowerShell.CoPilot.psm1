function Get-WhatsTheFix {
    Enter-Copilot -LastError
}

Set-Alias -Name wtf -Value Get-WhatsTheFix

function Enable-PSCopilotKeyHandler {
    param(
        [string]$Chord,
        [switch]$ReturnChord
    )

    $setHandler = $false
    if ('' -eq $Chord) {
        if ($null -eq ($handler = Get-PSReadLineKeyHandler -Bound | Where-Object { $_.Description.StartsWith('PSCopilot:') })) {
            for ($i = 3; $i -le 12; $i++) {
                $Chord = "F$i"
                if ($null -eq (Get-PSReadlineKeyHandler -Chord $Chord)) {
                    $setHandler = $true
                    break
                }
            }
        }
        else {
            $chord = $handler.Key
        }
    } else {
        $setHandler = $true
    }

    if ($setHandler) {
        Set-PSReadlineKeyHandler -Chord $Chord -Description 'PSCopilot: Enter PSCopilot chat mode' -ScriptBlock {
            Enter-Copilot
        }
    }

    if ($ReturnChord) {
        $Chord
    } else {
        Write-Host "PSCopilot registered for '$Chord'"
    }
}
