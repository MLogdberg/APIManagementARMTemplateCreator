using Newtonsoft.Json.Linq;
using System.Management.Automation;
using System.Text;

namespace APIManagementTemplate;

[Cmdlet(VerbsCommon.Get, "APIManagementTemplate", ConfirmImpact = ConfirmImpact.None)]
public class GeneratorCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true,HelpMessage = "Name of the API Management instance")]
    public string APIManagement;

    [Parameter(Mandatory = true,HelpMessage = "The name of the Resource Group")]
    public string ResourceGroup;

    [Parameter(Mandatory = false,HelpMessage = "The Subscription id (guid)")]
    public string SubscriptionId;

    [Parameter(Mandatory = false,HelpMessage = "Name of the Tenant i.e. contoso.onmicrosoft.com")]
    public string TenantName = "";

    //see filter in https://learn.microsoft.com/en-us/rest/api/apimanagement/apis/list-by-service
    [Parameter(Mandatory = false, HelpMessage = "Filter for what API's to exort i.e: path eq 'api/v1/currencyconverter' or endswith(path,'currencyconverter'). In addition to this, is it also possible to filter on productname i.e.: productname eq 'product-x'")]
    public string? APIFilters = null;
        
    [Parameter(Mandatory = false,HelpMessage = "Export AuthorizationServers")]
    public bool ExportAuthorizationServers = true;

    [Parameter(Mandatory = false,HelpMessage = "Export the API Management Instance")]
    public bool ExportPIManagementInstance = true;

    [Parameter(Mandatory = false,HelpMessage = "Export the API Management Certificates")]
    public bool ExportCertificates = true;

    [Parameter(Mandatory = false,HelpMessage = "Export the API Management Groups, not builtin")]
    public bool ExportGroups = true;

    [Parameter(Mandatory = false,HelpMessage = "Export the API Management Products")]
    public bool ExportProducts = true;

    [Parameter(Mandatory = false,HelpMessage = "Export the API Management Tags and API Tags")]
    public bool ExportTags = false;

    [Parameter(Mandatory = false,HelpMessage = "Export the API operations and schemas as a swagger/Open API 2.0 definition")]
    public bool ExportSwaggerDefinition = false;

    [Parameter(Mandatory = false,HelpMessage = "Export the API properties and backend url used in the API policy.")]
    public bool ExportApiPropertiesAndBackend = true;

    [Parameter(Mandatory = false,HelpMessage = "Export the API backend instances used in the API policy. Requires ExportApiPropertiesAndBackend to be true.")]
    public bool ExportBackendInstances = true;

    [Parameter(Mandatory = false,HelpMessage = "Export the Authorization Providers.")]
    public bool ExportAuthorizationProviders = false;

    [Parameter(Mandatory = false,HelpMessage = "List of named values to skip exporting. Has no effect if ExportApiPropertiesAndBackend is false")]
    public string[] IgnoreProperties = new string[0];
        
    [Parameter(Mandatory = false,HelpMessage = "A Bearer token value")]
    public string Token = "";

    [Parameter(Mandatory = false,HelpMessage = "Set to 'true' when all environment-specific parameters are defined as properties")]
    public bool ParametrizePropertiesOnly = false;

    [Parameter(Mandatory = false,HelpMessage = "Set to 'true' to replace the base-url of <set-backend-service> with a property")]
    public bool ReplaceSetBackendServiceBaseUrlWithProperty = false;

    [Parameter(Mandatory = false,HelpMessage = "If the parameter for the service name always should be called apimServiceName or depend on the name of the service")]
    public bool FixedServiceNameParameter = false;

    [Parameter(Mandatory = false,HelpMessage = "If the parameter for the keyvault name always should be called keyVaultName or depend on the name of the nameValue")]
    public bool FixedKeyVaultNameParameter = false;

    [Parameter(Mandatory = false,HelpMessage = "If an Application Insights instance should be created. Otherwise you need to provide the instrumentation key of an existing Application Insights instance as a parameter")]
    public bool CreateApplicationInsightsInstance = false;

    [Parameter(Mandatory = false, HelpMessage = "If set, result from rest interface will be saved to this folder")]
    public string DebugOutPutFolder = "";

    [Parameter(Mandatory = false,HelpMessage = "Filter API version")]
    public string ApiVersion = "";

    [Parameter(Mandatory = false, HelpMessage = "Piped input from armclient", ValueFromPipeline = true)]
    public string? ClaimsDump;

    [Parameter(Mandatory = false, HelpMessage = "Set to 'true' if you want the backend function key to be parameterized.")]
    public bool ParameterizeBackendFunctionKey = false;

    [Parameter(Mandatory = false, HelpMessage = "SeparatePolicyOutputFolder.")]
    public string SeparatePolicyOutputFolder = "";

    [Parameter(Mandatory = false, HelpMessage = "Set to 'true' if you get the error 'Operation on the API is in progress'. This option chains the product apis in order to reduce parallelism.")]
    public bool ChainDependencies = false;

    protected override void ProcessRecord()
    {
        AzureResourceCollector resourceCollector = new AzureResourceCollector();

        if (!string.IsNullOrEmpty(DebugOutPutFolder))
            resourceCollector.DebugOutputFolder = DebugOutPutFolder;

        if (ClaimsDump == null)
        {
            resourceCollector.token = String.IsNullOrEmpty(Token) ? resourceCollector.Login(TenantName) : Token;
        }
        else if (ClaimsDump.Contains("Token copied"))
        {
            Token = Clipboard.GetText().Replace("Bearer ", "");
            resourceCollector.token = Token;
        }
        else
        {
            return;
        }

        try
        {
            TemplateGenerator generator = new TemplateGenerator(APIManagement, SubscriptionId, ResourceGroup,
                APIFilters, ExportGroups, ExportProducts, ExportPIManagementInstance, ParametrizePropertiesOnly,
                resourceCollector, replaceSetBackendServiceBaseUrlAsProperty: ReplaceSetBackendServiceBaseUrlWithProperty, fixedServiceNameParameter: FixedServiceNameParameter,
                createApplicationInsightsInstance: CreateApplicationInsightsInstance, apiVersion: ApiVersion, parameterizeBackendFunctionKey: ParameterizeBackendFunctionKey,
                exportSwaggerDefinition: ExportSwaggerDefinition, exportCertificates: ExportCertificates, exportTags: ExportTags, separatePolicyOutputFolder: SeparatePolicyOutputFolder,
                chainDependencies: ChainDependencies, exportApiPropertiesAndBackend: ExportApiPropertiesAndBackend, fixedKeyVaultNameParameter: FixedKeyVaultNameParameter, exportBackendInstances: ExportBackendInstances,
                ignoreProperties: IgnoreProperties, exportAuthorizationProviders:ExportAuthorizationProviders);
            JObject result = generator.GenerateTemplate().Result;
            WriteObject(result.ToString());
        }
        catch (Exception ex)
        {
            if (ex is AggregateException)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Aggregation exception thrown, se following exceptions for more information");
                AggregateException ae = (AggregateException)ex;
                foreach (var e in ae.InnerExceptions)
                {
                    sb.AppendLine($"Exception: {e.Message}");
                    sb.AppendLine($"{e.StackTrace}");
                    sb.AppendLine("-------------------------------------------");
                }
                WriteObject(sb.ToString());
                throw new Exception($"Aggregation Exception thrown, {ae.Message}, first Exception message is: {ae.InnerExceptions.First().Message}, for more information read the output file.");
            }
            else
            {
                throw ex;
            }
        }
    }
}