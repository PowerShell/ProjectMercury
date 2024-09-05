@{
    RootModule        = "packaging.psm1"
    GUID              = "27f898a4-1fcd-4704-8fd1-16894ab80090"
    Author            = "AIShell"
    CompanyName       = "Microsoft Corporation"
    Copyright         = "Copyright (c) Microsoft Corporation."
    ModuleVersion     = "1.0.0"
    PowerShellVersion = "7.2"
    CmdletsToExport   = @()
    FunctionsToExport = @(
        'New-NugetPackage'
        'New-NuSpec'
        'Get-ProjectPackageInformation'
        'New-TarballPackage'
        'New-ZipPackage'
        'New-MSIXPackage'
    )
}
