## API Management ARM Template Creator

This is a PowerShell script module to extract API Management to ARM templates, focus is to provide a module for easy deployments.  

### How to use
**Install from PowerShell Gallery**  
`PS> Install-Module -Name APIManagementTemplate`

**Update to latest version from PowerShell Gallery**  
`PS> Update-Module -Name APIManagementTemplate`

Install-Module is part of PowerShellGet which is included on Windows 10 and Windows Server 2016. See [this](https://docs.microsoft.com/en-us/powershell/gallery/installing-psget) link for installation instructions on older platforms.

**Import without installing**  
Clone the project, open, and build.

Open PowerShell and Import the module:

`Import-Module C:\{pathToSolution}\APIManagementARMTemplateCreator\APIManagementTemplate\bin\Debug\APIManagementTemplate.dll`

Run the PowerShell command `Get-APIManagementTemplate`.  You can pipe the output as needed, and recommended you pipe in a token from `armclient`

`armclient token 80d4fe69-xxxx-4dd2-a938-9250f1c8ab03 | Get-APIManagementTemplate -APIManagement MyApiManagementInstance -ResourceGroup myResourceGroup -SubscriptionId 80d4fe69-xxxx-4dd2-a938-9250f1c8ab03 | Out-File C:\template.json`

Example when user is connected to multitenants:

`Get-APIManagementTemplate -APIManagement MyApiManagementInstance -ResourceGroup myResourceGroup -SubscriptionId 80d4fe69-xxxx-4dd2-a938-9250f1c8ab03 -TenantName contoso.onmicrosoft.com`

### Specifications

| Parameter | Description | Required |Default |
| --------- | ---------- | -------| --- |
| APIManagement | Name of the API Management instance| true | |
| ResourceGroup | The name of the Resource Group | true | |
| SubscriptionId | The Subscription id (guid)| true | |
| TenantName | Name of the Tenant i.e. contoso.onmicrosoft.com | false | |
| APIFilters | Filter for what API's to exort i.e: path eq 'api/v1/currencyconverter' or endswith(path,'currencyconverter') | false | |
| ExportAuthorizationServers | Flag inidicating if Authroization servers should be exported, default true | false | true | 
| ExportPIManagementInstance | Flag inidicating if the API Management instance should be exported, default true | false | true |
| ExportGroups | Flag inidicating if Groups should be exported, default true | false | true |
| ExportProducts | Flag inidicating if Products should be exported, default true | false | true |
| Token | An AAD Token to access the resources - should not include `Bearer`, only the token | false | |
| ExportSwaggerDefinition | Export the API operations and schemas as a swagger/Open API 2.0 definition. If set to false then the operations and schemas of the API will be included as arm templates  | false | false |
| Token | An AAD Token to access the resources - should not include `Bearer`, only the token | false | |
| ParametrizePropertiesOnly | If parameters only should be created for properties such as names of apim services or logic apps and not names of groups, apis or products | false | false |
| ReplaceSetBackendServiceBaseUrlWithProperty | If the base-url of <set-backend-service> with should be replaced with a property instead of a parameter. If this is false you will not be able to set SeparatePolicyFile=true for Write-APIManagementTemplates when you have set-backend-service with base-url-attribute in a policy | false | false |
| FixedServiceNameParameter | True if the parameter for the name of the service should have a fixed name (apimServiceName). Otherwise the parameter name will depend on the name of the service (service_PreDemoTest_name)| false | false |
| CreateApplicationInsightsInstance | If an Application Insights instance should be created when used by a logger. Otherwise you need to provide the instrumentation key of an existing Application Insights instance as a parameter| false | false |
| DebugOutPutFolder | If set, result from rest interface will be saved to this folder | false | |
| ApiVersion | If set, api result will be filtered based on this value i.e: v2 | false | |
| ClaimsDump | A dump of claims piped in from `armclient` - should not be manually set | false |
| ParameterizeBackendFunctionKey | Set to 'true' if you want the backend function key to be parameterized, default false. | false | false |

After extraction a parameters file can be created off the ARMTemplate.

### Multiple small ARM-templates with Write-APIManagementTemplates
Use Write-APIManagementTemplates generate many small ARM templates (as suggested in https://github.com/Azure/azure-api-management-devops-example) instead of one big ARM template.

`armclient token 80d4fe69-xxxx-4dd2-a938-9250f1c8ab03 | Get-APIManagementTemplate -APIManagement MyApiManagementInstance -ResourceGroup myResourceGroup -SubscriptionId 80d4fe69-xxxx-4dd2-a938-9250f1c8ab03 | Write-APIManagementTemplates -OutputDirectory C:\temp\templates -SeparatePolicyFile $true`

### Specifications

| Parameter | Description | Required | Default | 
| --------- | ---------- | -------| --- |
| ApiStandalone | If the APIs should be able to be deployed independently of the rest of the resources | false | true | 
| OutputDirectory | The directory where the templates are written to | false | . | 
| SeparatePolicyFile | If the policies should be written to a separate xml file. If set to false then the policies are included as a part of the arm templates | false | false | 
| SeparateSwaggerFile | Swagger/Openapi definitions are written to a separate file. If set to false then the Swagger/Openapi definitions are included as part of the arm templates | false | false | 
| MergeTemplates | If the template already exists in the output directory, it will be merged with the new result. | false | false | 
| GenerateParameterFiles | If parameter files should be generated | false | false | 
| ReplaceListSecretsWithParameter | If the key to an Azure Function should be defined in a parameter instead of calling listsecrets | false | false |
| DebugTemplateFile | If set, the input ARM template is written to this file | false | |
| ARMTemplate | The ARM template piped from Get-APIManagementTemplate - should not be manually set | false | |
