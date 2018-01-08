#if you have problem with execution policy execute this in a administrator runned powershell window.
Set-ExecutionPolicy -ExecutionPolicy Unrestricted

Import-Module "C:\Samples\APIManagementARMTemplateCreator\APIManagementTemplate\bin\Debug\APIManagementTemplate.dll"


#Set the name of the API Mangement instance
$apimanagementname = 'AgrifirmAPIMdevtest'

#Set the resource group 
$resourcegroupname = 'Dev.ApiManagement' # 'app-tst-cs-int-Migrated'
#Set the subscription id 
$subscriptionid = '282d71e4-f66b-4e8f-8e49-4faea8667362' #'d4baa1e9-15f5-4c85-bb3e-1e108dc79b00'#
#Set the tenant to use when login ing, make sure it has the right tennant
$tenant = 'agrifirm.onmicrosoft.com'

#optional set filter for a specific api (using standard REST filter, with path we can select api based on the API path)
#$filter = "path eq 'api/v1/order'"
#$filter = "path eq 'api/v1/currencyconverter'"

 
#setting the output filename
$filenname = 'C:\Samples\APIManagementARMTemplateCreator\' + $apimanagementname + '.json'

Get-APIManagementTemplate -APIFilters $filter -APIManagement $apimanagementname -ResourceGroup $resourcegroupname -SubscriptionId $subscriptionid -TenantName $tenant -ExportPIManagementInstance $false -ParametrizePropertiesOnly $true | Out-File $filenname