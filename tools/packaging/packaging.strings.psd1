@{
    NuspecTemplate = @'
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
    <metadata>
        <id>AIShell.Abstraction</id>
        <version>{0}</version>
        <authors>Microsoft</authors>
        <projectUrl>https://github.com/PowerShell/AIShell</projectUrl>
        <requireLicenseAcceptance>false</requireLicenseAcceptance>
        <license type="expression">MIT</license>
        <licenseUrl>https://licenses.nuget.org/MIT</licenseUrl>
        <description>The abstraction layer SDK for building a plugin agent for AIShell.</description>
        <copyright>&#169; Microsoft Corporation. All rights reserved.</copyright>
        <tags>AIShell</tags>
        <language>en-US</language>
        <dependencies>
            <group targetFramework="net8.0"></group>
        </dependencies>
    </metadata>
</package>
'@
}
