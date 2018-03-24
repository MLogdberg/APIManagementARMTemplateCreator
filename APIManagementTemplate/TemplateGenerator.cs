using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Management.Automation;
using APIManagementTemplate.Models;
using Newtonsoft.Json.Linq;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace APIManagementTemplate
{
    public class TemplateGenerator
    {

        private string subscriptionId;
        private string resourceGroup;
        private string servicename;
        private string apiFilters;


        private bool exportPIManagementInstance;
        private bool exportGroups;
        private bool exportProducts;
        private bool parametrizePropertiesOnly;

        IResourceCollector resourceCollector;
        public TemplateGenerator(string servicename, string subscriptionId, string resourceGroup, string apiFilters, bool exportGroups, bool exportProducts, bool exportPIManagementInstance, bool parametrizePropertiesOnly, IResourceCollector resourceCollector)
        {
            this.servicename = servicename;
            this.subscriptionId = subscriptionId;
            this.resourceGroup = resourceGroup;
            this.apiFilters = apiFilters;
            this.exportGroups = exportGroups;
            this.exportProducts = exportProducts;
            this.exportPIManagementInstance = exportPIManagementInstance;
            this.parametrizePropertiesOnly = parametrizePropertiesOnly;
            this.resourceCollector = resourceCollector;
        }

        private string GetAPIMResourceIDString()
        {
            return $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.ApiManagement/service/{servicename}";
        }

        public async Task<JObject> GenerateTemplate()
        {
            DeploymentTemplate template = new DeploymentTemplate(this.parametrizePropertiesOnly);
            if (exportPIManagementInstance)
            {
                var apim = await resourceCollector.GetResource(GetAPIMResourceIDString());
                template.AddAPIManagementInstance(apim);
            }

            var apis = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/apis", (string.IsNullOrEmpty(apiFilters) ? "" : $"$filter={apiFilters}"));
            foreach (JObject apiObject in (apis == null ? new JArray() : apis.Value<JArray>("value")))
            {

                var id = apiObject.Value<string>("id");
                var apiInstance = await resourceCollector.GetResource(id);

                var apiTemplateResource = template.AddApi(apiInstance);

                var operations = await resourceCollector.GetResource(id + "/operations");
                foreach (JObject operation in (operations == null ? new JArray() : operations.Value<JArray>("value")))
                {
                    var opId = operation.Value<string>("id");

                    var operationInstance = await resourceCollector.GetResource(opId);
                    var operationTemplateResource = template.CreateOperation(operationInstance);
                    apiTemplateResource.Value<JArray>("resources").Add(operationTemplateResource);


                    var operationPolicies = await resourceCollector.GetResource(opId + "/policies");
                    foreach (JObject policy in (operationPolicies == null ? new JArray() : operationPolicies.Value<JArray>("value")))
                    {
                        var pol = template.CreatePolicy(policy);

                        //add properties
                        this.PolicyHandleProperties(pol, apiTemplateResource.Value<string>("name"));

                        //Handle Azure Resources
                        if (!this.PolicyHandeAzureResources(pol, apiTemplateResource.Value<string>("name"), template))
                        {
                            var operationSuffix = apiInstance.Value<string>("name") + "_" + operationInstance.Value<string>("name");
                            PolicyHandeBackendUrl(pol, operationSuffix, template);
                        }
                        
                        operationTemplateResource.Value<JArray>("resources").Add(pol);
                        //handle nextlink?
                    }
                    //handle nextlink?                
                }

                var apiPolicies = await resourceCollector.GetResource(id + "/policies");
                foreach (JObject policy in (apiPolicies == null ? new JArray() : apiPolicies.Value<JArray>("value")))
                {
                    //Handle SOAP Backend
                    var backendid = TemplateHelper.GetBackendIdFromnPolicy(policy["properties"].Value<string>("policyContent"));


                    var policyTemplateResource = template.CreatePolicy(policy);
                    PolicyHandeBackendUrl(policy, apiInstance.Value<string>("name"), template);
                    this.PolicyHandleProperties(policy, apiTemplateResource.Value<string>("name"));
                    apiTemplateResource.Value<JArray>("resources").Add(policyTemplateResource);



                    if (!string.IsNullOrEmpty(backendid))
                    {
                        var backendInstance = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/backends/" + backendid);
                        var property = template.AddBackend(backendInstance);

                        if(property != null)
                        {
                            var idp = this.identifiedProperties.Where(pp => pp.name.Contains(property.name + "_manual-invoke_")).FirstOrDefault();
                            idp.extraInfo = property.extraInfo;
                            idp.type = Property.PropertyType.LogicAppRevisionGa;
                        }


                        if (apiTemplateResource.Value<JArray>("dependsOn") == null)
                            apiTemplateResource["dependsOn"] = new JArray();

                        //add dependeOn
                        apiTemplateResource.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/backends', parameters('service_{servicename}_name'), parameters('backend_{backendInstance.Value<string>("name")}_name'))]");
                    }

                    //handle nextlink?
                }

                //handle nextlink?
            }

            // Export all groups if we don't export the products.
            if (exportGroups && !exportProducts)
            {
                var groups = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/groups");
                foreach (JObject groupObject in (groups == null ? new JArray() : groups.Value<JArray>("value")))
                {
                    //cannot edit och create built in groups
                    if (groupObject["properties"].Value<bool>("builtIn") == false)
                        template.AddGroup(groupObject);
                }
            }

            if (exportProducts)
            {
                var products = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/products");
                foreach (JObject productObject in (products == null ? new JArray() : products.Value<JArray>("value")))
                {
                    var id = productObject.Value<string>("id");
                    var productInstance = await resourceCollector.GetResource(id);

                    var productApis = await resourceCollector.GetResource(id + "/apis", (string.IsNullOrEmpty(apiFilters) ? "" : $"$filter={apiFilters}"));

                    // Skip product if not related to an API in the filter.
                    if (productApis != null && productApis.Value<JArray>("value").Count > 0)
                    {
                        var productTemplateResource = template.AddProduct(productObject);

                        foreach (JObject productApi in (productApis == null ? new JArray() : productApis.Value<JArray>("value")))
                        {
                            productTemplateResource.Value<JArray>("resources").Add(template.AddProductSubObject(productApi));
                        }

                        var groups = await resourceCollector.GetResource(id + "/groups");
                        foreach (JObject group in (groups == null ? new JArray() : groups.Value<JArray>("value")))
                        {
                            if (group["properties"].Value<bool>("builtIn") == false)
                            {
                                // Add group resource
                                var groupObject = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/groups/" + group.Value<string>("name"));
                                template.AddGroup(groupObject);
                                productTemplateResource.Value<JArray>("resources").Add(template.AddProductSubObject(group));
                                productTemplateResource.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/groups', parameters('service_{servicename}_name'), '{group.Value<string>("name")}')]");
                            }
                        }
                    }
                }
            }

            var properties = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/properties");
            foreach (JObject propertyObject in (properties == null ? new JArray() : properties.Value<JArray>("value")))
            {

                var id = propertyObject.Value<string>("id");
                var name = propertyObject["properties"].Value<string>("displayName");

                var identifiedProperty = this.identifiedProperties.Where(idp => name.EndsWith(idp.name)).FirstOrDefault();
                if (identifiedProperty != null)
                {
                    if (identifiedProperty.type == Property.PropertyType.LogicApp)
                    {
                        propertyObject["properties"]["value"] = $"[concat('sv=',{identifiedProperty.extraInfo}.queries.sv,'&sig=',{identifiedProperty.extraInfo}.queries.sig)]";
                    }
                    else if (identifiedProperty.type == Property.PropertyType.LogicAppRevisionGa)
                    {
                        propertyObject["properties"]["value"] = $"[{identifiedProperty.extraInfo}.queries.sig]";
                    }
                    template.AddProperty(propertyObject);

                    if (!parametrizePropertiesOnly)
                    {
                        foreach (var apiName in identifiedProperty.apis)
                        {
                            var apiTemplate = template.resources.Where(rr => rr.Value<string>("name") == apiName).FirstOrDefault();
                            apiTemplate.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/properties', parameters('service_{servicename}_name'),parameters('property_{propertyObject.Value<string>("name")}_name'))]");
                        }
                    }
                }
            }

            return JObject.FromObject(template);

        }

        public void PolicyHandleProperties(JObject policy, string apiname)
        {
            var policyContent = policy["properties"].Value<string>("policyContent");
            var match = Regex.Match(policyContent, "{{(?<name>[-_.a-zA-Z0-9]*)}}");

            while (match.Success)
            {
                string name = match.Groups["name"].Value;
                var idp = identifiedProperties.Where(pp => pp.name == name).FirstOrDefault();
                if (idp == null)
                {
                    this.identifiedProperties.Add(new Property() { name = name, type = Property.PropertyType.Standard, apis = new List<string>(new string[] { apiname }) });
                }
                else if (!idp.apis.Contains(apiname))
                {
                    idp.apis.Add(apiname);
                }
                match = match.NextMatch();
            }

        }

        public List<Property> identifiedProperties = new List<Property>();

        public bool PolicyHandeAzureResources(JObject policy, string apiname, DeploymentTemplate template)
        {
            var policyContent = policy["properties"].Value<string>("policyContent");
            var policyXMLDoc = XDocument.Parse(policyContent);

            var commentMatch = Regex.Match(policyContent, "<!--[ ]*(?<json>{+.*\"azureResource.*)-->");
            if (commentMatch.Success)
            {

                var json = commentMatch.Groups["json"].Value;

                JObject azureResourceObject = JObject.Parse(json).Value<JObject>("azureResource");
                if (azureResourceObject != null)
                {
                    string reourceType = azureResourceObject.Value<string>("type");
                    string id = azureResourceObject.Value<string>("id");

                    if (reourceType == "logicapp")
                    {
                        var logicAppNameMatch = Regex.Match(id, @"resourceGroups/(?<resourceGroupName>[\w-_d]*)/providers/Microsoft.Logic/workflows/(?<name>[\w-_d]*)/triggers/(?<triggerName>[\w-_d]*)");
                        string logicAppName = logicAppNameMatch.Groups["name"].Value;
                        string logicApptriggerName = logicAppNameMatch.Groups["triggerName"].Value;
                        string logicAppResourceGroup = logicAppNameMatch.Groups["resourceGroupName"].Value;

                        string listCallbackUrl = $"listCallbackUrl(resourceId(parameters('{template.AddParameter($"logicApp_{logicAppName}_resourcegroup", "string", logicAppResourceGroup)}'),'Microsoft.Logic/workflows/triggers', parameters('{template.AddParameter($"logicApp_{logicAppName}_name", "string", logicAppName)}'),parameters('{template.AddParameter($"logicApp_{logicAppName}_trigger", "string", logicApptriggerName)}')), providers('Microsoft.Logic', 'workflows').apiVersions[0])";

                        //Set the Base URL
                        var backendService = policyXMLDoc.Descendants().Where(dd => dd.Name == "set-backend-service" && dd.Attribute("id").Value == "apim-generated-policy").FirstOrDefault();
                        policy["properties"]["policyContent"] = CreatePolicyContentReplaceBaseUrl(backendService, policyContent, $"{listCallbackUrl}.basePath");

                        //Handle the sig property
                        var rewriteElement = policyXMLDoc.Descendants().Where(dd => dd.Name == "rewrite-uri").LastOrDefault();
                        var rewritetemplate = rewriteElement.Attribute("template");
                        if (rewritetemplate != null)
                        {

                            var match = Regex.Match(rewritetemplate.Value, "{{(?<name>[-_.a-zA-Z0-9]*)}}");

                            if (match.Success)
                            {
                                string propname = match.Groups["name"].Value;
                                this.identifiedProperties.Add(new Property()
                                {
                                    type = Property.PropertyType.LogicApp,
                                    name = propname,
                                    extraInfo = listCallbackUrl,
                                    apis = new List<string>(new string[] { apiname })
                                });
                            }
                        }
                        /*
                        <policies>
                          <inbound>
                            <rewrite-uri id="apim-generated-policy" template="?api-version=2016-06-01&amp;sp=/triggers/request/run&amp;{{orderrequest59a6b4783fb21a7984df42ae}}" />
                            <set-backend-service id="apim-generated-policy" base-url="https://prod-48.westeurope.logic.azure.com/workflows/bc406236bfff482a836ca4f6caabbb17/triggers/request/paths/invoke" />
                            <base />
                            <set-header name="Ocp-Apim-Subscription-Key" exists-action="delete" />
                          </inbound>
                          <outbound>
                            <base />
                          </outbound>
                          <backend>
                            <base />
                            <!-- { "azureResource": { "type": "logicapp", "id": "/subscriptions/c107df29-a4af-4bc9-a733-f88f0eaa4296/resourceGroups/PreDemoTest/providers/Microsoft.Logic/workflows/INT001-GetOrderInfo/triggers/request" } } -->
                          </backend>
                        </policies>
                        */

                    }
                    else if (reourceType == "funcapp")
                    {
                        /*
                        var logicAppNameMatch = Regex.Match(id, "resourceGroups/(?<resourceGroupName>.*)/providers/Microsoft.Logic/workflows/(?<name>.*)/triggers/(?<triggerName>.*)");
                        string functionAppName = logicAppNameMatch.Groups["name"].Value;
                        string functionName = logicAppNameMatch.Groups["triggerName"].Value;
                        string functionResourceGroup = logicAppNameMatch.Groups["resourceGroupName"].Value;*/
                    }
                }

            }
            return commentMatch.Success;
        }
        public void PolicyHandeBackendUrl(JObject policy, string apiname, DeploymentTemplate template)
        {
            var policyContent = policy["properties"].Value<string>("policyContent");
            var policyXMLDoc = XDocument.Parse(policyContent);
            //find the last backend service and add as parameter
            var backendService = policyXMLDoc.Descendants().Where(dd => dd.Name == "set-backend-service").LastOrDefault();
            if (backendService != null)
            {

                if (backendService.Attribute("base-url") != null)
                {
                    var baseUrl = backendService.Attribute("base-url");
                    var paramname = template.AddParameter($"api_{apiname}_backendurl", "string", backendService.Attribute("base-url").Value);
                    policy["properties"]["policyContent"] = CreatePolicyContentReplaceBaseUrl(backendService, policyContent, $"parameters('{paramname}')");
                }

            }

        }


        private string CreatePolicyContentReplaceBaseUrl(XElement backendService, string policyContent, string replaceText)
        {
            var baseUrl = backendService.Attribute("base-url");
            if (baseUrl != null && policyContent.IndexOf(baseUrl.Value) > -1)
            {
                int index = policyContent.IndexOf(baseUrl.Value);

                return "[Concat('" + policyContent.Substring(0, index) + "'," + replaceText + ",'" + policyContent.Substring(index + baseUrl.Value.Length) + "')]";
            }
            return policyContent;
        }

    }



}
