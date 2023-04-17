function Get-WhatsTheFix {
    Enter-Copilot -LastError
}

Set-Alias -Name wtf -Value Get-WhatsTheFix

function Enable-PSCopilotKeyHandler {
    [CmdletBinding()]
    param(
        [switch]$ReturnChord
    )

    if ($null -eq ($handler = Get-PSReadLineKeyHandler -Bound | Where-Object { $_.Description.StartsWith('PSCopilot:') })) {
        for ($i = 3; $i -le 12; $i++) {
            $chord = "F$i"
            if ($null -eq (Get-PSReadlineKeyHandler -Chord $chord)) {
                Set-PSReadlineKeyHandler -Chord $chord -Description 'PSCopilot: Enter PSCopilot chat mode' -ScriptBlock {
                    Enter-Copilot
                }
                break
            }
        }
    }
    else {
        $chord = $handler.Key
    }

    if ($ReturnChord) {
        $chord
    } else {
        Write-Host "PSCopilot registered for '$chord'"
    }
}
