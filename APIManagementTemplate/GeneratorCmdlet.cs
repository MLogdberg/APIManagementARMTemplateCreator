using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace APIManagementTemplate
{
    [Cmdlet(VerbsCommon.Get, "APIManagementTemplate", ConfirmImpact = ConfirmImpact.None)]
    public class GeneratorCmdlet : PSCmdlet
    {
        [Parameter(
            Mandatory = true,
            HelpMessage = "Name of the API Management instance"
            )]
        public string APIManagement;

        [Parameter(
            Mandatory = true,
            HelpMessage = "The name of the Resource Group"
            )]
        public string ResourceGroup;

        [Parameter(
            Mandatory = false,
            HelpMessage = "The Subscription id (guid)"
            )]
        public string SubscriptionId;

        [Parameter(
            Mandatory = false,
            HelpMessage = "Name of the Tenant i.e. contoso.onmicrosoft.com"
        )]
        public string TenantName = "";

        //see filter in https://docs.microsoft.com/en-us/rest/api/apimanagement/api/listbyservice
        [Parameter(
            Mandatory = false,
            HelpMessage = "Filter for what API's to exort i.e: path eq 'api/v1/currencyconverter' or endswith(path,'currencyconverter')",
            ValueFromPipeline = true
        )]
        public string APIFilters = null;

        [Parameter(
            Mandatory = false,
            HelpMessage = "Export AuthorizationServers",
            ValueFromPipeline = true
        )]
        public bool ExportAuthorizationServers = true;

        [Parameter(
            Mandatory = false,
            HelpMessage = "Export the API Management Instance",
            ValueFromPipeline = true
        )]
        public bool ExportPIManagementInstance = true;

        [Parameter(
            Mandatory = false,
            HelpMessage = "Export the API Management Groups, not builtin",
            ValueFromPipeline = true
        )]
        public bool ExportGroups = true;

        [Parameter(
            Mandatory = false,
            HelpMessage = "Export the API Management Products",
            ValueFromPipeline = true
        )]
        public bool ExportProducts = true;

        [Parameter(
            Mandatory = false,
            HelpMessage = "Piped input from armclient",
            ValueFromPipeline = true
        )]
        public string Token = "";

        [Parameter(
            Mandatory = false,
            HelpMessage = "Set to 'true' when all environment-specific parameters are defined as properties",
            ValueFromPipeline = true
        )]
        public bool ParametrizePropertiesOnly = false;

        public string DebugOutPutFolder = "";
        [Parameter(
            Mandatory = false,
            HelpMessage = "If set, result from rest interface will be saved to this folder",
            ValueFromPipeline = true
        )]


        public string ClaimsDump;

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
                TemplateGenerator generator = new TemplateGenerator(APIManagement, SubscriptionId, ResourceGroup, APIFilters, ExportGroups, ExportProducts, ExportPIManagementInstance, ParametrizePropertiesOnly, resourceCollector);
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
}
