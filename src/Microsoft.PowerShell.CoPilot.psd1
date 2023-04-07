# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

@{
    RootModule = '.\Microsoft.PowerShell.CoPilot.psm1'
    NestedModules = @('.\Microsoft.PowerShell.CoPilot.dll')
    ModuleVersion = '0.1.0'
    CompatiblePSEditions = @('Core')
    GUID = '4e30f432-98c2-4bf9-b6bd-b5514c704f52'
    Author = 'Microsoft Corporation'
    CompanyName = 'Microsoft Corporation'
    Copyright = '(c) Microsoft Corporation. All rights reserved.'
    Description = "This module enables an AI chat mode to work interactively with large language models."
    PowerShellVersion = '7.0'
    CmdletsToExport = @(
        'Enter-CoPilot', 'Enable-PSCoPilotKeyHandler', 'Get-WhatsTheFix'
    )
    AliasesToExport = @(
        'copilot', 'wtf'
    )
    PrivateData = @{
        PSData = @{
            LicenseUri = 'https://github.com/SteveL-MSFT/PSAIChat/blob/main/LICENSE'
            ProjectUri = 'https://github.com/SteveL-MSFT/PSAIChat'
            ReleaseNotes = 'Initial release'
            Prerelease = 'Preview1'
        }
    }

    # HelpInfoURI = ''
}
