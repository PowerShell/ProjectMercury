@{
    RootModule = 'Aish.psm1'
    NestedModules = @("ShellCopilot.Integration.dll")
    ModuleVersion = '0.1.0'
    GUID = 'ECB8BEE0-59B9-4DAE-9D7B-A990B480279A'
    Author = 'Microsoft Corporation'
    CompanyName = 'Microsoft Corporation'
    Copyright = '(c) Microsoft Corporation. All rights reserved.'
    Description = 'Integration with the AISH to provide intelligent shell experience'
    PowerShellVersion = '7.4'
    FunctionsToExport = @()
    CmdletsToExport = @('Start-Aish','Invoke-Aish','Resolve-Error')
    VariablesToExport = '*'
    AliasesToExport = @('aish', 'askai', 'fixit')
}
