function Get-WhatsTheFix {
    Enter-CoPilot -LastError
}

Set-Alias -Name wtf -Value Get-WhatsTheFix

function Enable-PSCoPilotKeyHandler {
    [CmdletBinding()]
    param(
        [switch]$ReturnChord
    )

    if ($null -eq ($handler = Get-PSReadLineKeyHandler -Bound | Where-Object { $_.Description.StartsWith('PSCoPilot:') })) {
        for ($i = 3; $i -le 12; $i++) {
            $chord = "F$i"
            if ($null -eq (Get-PSReadlineKeyHandler -Chord $chord)) {
                Set-PSReadlineKeyHandler -Chord $chord -Description 'PSCoPilot: Enter PSCopilot chat mode' -ScriptBlock {
                    Invoke-CoPilot
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
        Write-Host "PSCoPilot registered for '$chord'"
    }
}
