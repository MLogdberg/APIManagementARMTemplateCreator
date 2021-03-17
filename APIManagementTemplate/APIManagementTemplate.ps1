param([string] $version, [string] $apikey)

$binPath = Join-Path $PSScriptRoot "bin\Release"
$modulePath = Join-Path $PSScriptRoot "bin\APIManagementTemplate"

$manifestPath = Join-Path $PSScriptRoot "APIManagementTemplate.psd1"
$manifest = Test-ModuleManifest -Path $manifestPath

Update-ModuleManifest -Path $manifestPath -CmdletsToExport 'Get-ParameterTemplate','Get-APIManagementTemplate' -ModuleVersion $version

Write-Host "Preparing module"

New-Item $modulePath -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $binPath "*.dll") $modulePath
Copy-Item (Join-Path $PSScriptRoot "APIManagementTemplate.psd1") (Join-Path $modulePath "APIManagementTemplate.psd1")

Write-Host "Publishing module"

Publish-Module -Path $modulePath -Repository PSGallery -NuGetApiKey $apikey

Remove-Item $modulePath -Force -Recurse
