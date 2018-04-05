$binPath = Join-Path $PSScriptRoot "bin\Release"
$modulePath = Join-Path $PSScriptRoot "bin\APIManagementTemplate"

$manifestPath = Join-Path $PSScriptRoot "APIManagementTemplate.psd1"
$manifest = Test-ModuleManifest -Path $manifestPath

Write-Host "Module version needs to be incremented before publishing. Current version is $($manifest.Version)"

Update-ModuleManifest -Path $manifestPath -CmdletsToExport '*' -ModuleVersion (Read-Host "New module version: ")

Write-Host "Preparing module"

New-Item $modulePath -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $binPath "*.dll") $modulePath
Copy-Item (Join-Path $PSScriptRoot "APIManagementTemplate.psd1") (Join-Path $modulePath "APIManagementTemplate.psd1")

Write-Host "Publishing module"

Publish-Module -Path $modulePath -Repository PSGallery -NuGetApiKey (Read-Host "NuGetApiKey (from https://powershellgallery.com/account): ")

Remove-Item $modulePath -Force -Recurse
