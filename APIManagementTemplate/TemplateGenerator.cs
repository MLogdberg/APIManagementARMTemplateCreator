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
        private bool replaceSetBackendServiceBaseUrlAsProperty;
        private bool fixedServiceNameParameter;
        private bool createApplicationInsightsInstance;
        private string apiVersion;
        private readonly bool parameterizeBackendFunctionKey;
        private readonly bool exportSwaggerDefinition;
        IResourceCollector resourceCollector;

        public TemplateGenerator(string servicename, string subscriptionId, string resourceGroup, string apiFilters, bool exportGroups, bool exportProducts, bool exportPIManagementInstance, bool parametrizePropertiesOnly, IResourceCollector resourceCollector, bool replaceSetBackendServiceBaseUrlAsProperty = false, bool fixedServiceNameParameter = false, bool createApplicationInsightsInstance = false, string apiVersion = null, bool parameterizeBackendFunctionKey = false, bool exportSwaggerDefinition = false)
        {
            this.servicename = servicename;
            this.subscriptionId = subscriptionId;
            this.resourceGroup = resourceGroup;
            this.apiFilters = apiFilters;
            this.exportGroups = exportGroups;
            this.exportProducts = exportProducts;
            this.exportPIManagementInstance = exportPIManagementInstance;
            this.parametrizePropertiesOnly = parametrizePropertiesOnly;
            this.replaceSetBackendServiceBaseUrlAsProperty = replaceSetBackendServiceBaseUrlAsProperty;
            this.resourceCollector = resourceCollector;
            this.fixedServiceNameParameter = fixedServiceNameParameter;
            this.createApplicationInsightsInstance = createApplicationInsightsInstance;
            this.apiVersion = apiVersion;
            this.parameterizeBackendFunctionKey = parameterizeBackendFunctionKey;
            this.exportSwaggerDefinition = exportSwaggerDefinition;
        }

        private string GetAPIMResourceIDString()
        {
            return $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.ApiManagement/service/{servicename}";
        }

        public async Task<JObject> GenerateTemplate()
        {
            DeploymentTemplate template = new DeploymentTemplate(this.parametrizePropertiesOnly, this.fixedServiceNameParameter, this.createApplicationInsightsInstance, this.parameterizeBackendFunctionKey);
            if (exportPIManagementInstance)
            {
                var apim = await resourceCollector.GetResource(GetAPIMResourceIDString());
                var apimTemplateResource = template.AddAPIManagementInstance(apim);
                await AddServiceResource(apimTemplateResource, "/policies", policy =>
                {
                    PolicyHandleProperties(policy, "Global", "Global");
                    return template.CreatePolicy(policy);
                });
                await AddServiceResource(apimTemplateResource, "/identityProviders",
                    identityProvider => template.CreateIdentityProvider(identityProvider, false));
                var loggers = await AddServiceResource(apimTemplateResource, "/loggers", logger =>
                {
                    bool isApplicationInsightsLogger = (logger["properties"]?["loggerType"]?.Value<string>() ?? string.Empty) == "applicationInsights";
                    if (createApplicationInsightsInstance && isApplicationInsightsLogger)
                        apimTemplateResource.Value<JArray>("resources").Add(template.AddApplicationInsightsInstance(logger));
                    if (!createApplicationInsightsInstance || !isApplicationInsightsLogger)
                        HandleProperties(logger.Value<string>("name"), "Logger", logger["properties"].ToString());
                    return template.CreateLogger(logger, false);
                });
                await AddServiceResource(apimTemplateResource, "/diagnostics",
                    diagnostic => template.CreateDiagnostic(diagnostic, loggers == null ? new JArray() : loggers.Value<JArray>("value"), false));
            }

            var apis = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/apis", (string.IsNullOrEmpty(apiFilters) ? "" : $"$filter={apiFilters}"));
            if (apis != null)
            {
                foreach (JObject apiObject in (!string.IsNullOrEmpty(apiVersion) ? apis.Value<JArray>("value").Where(aa => aa["properties"].Value<string>("apiVersion") == this.apiVersion) : apis.Value<JArray>("value")))
                {

                    var id = apiObject.Value<string>("id");


                    var apiInstance = await resourceCollector.GetResource(id);
                    var apiTemplateResource = template.AddApi(apiInstance);

                    //Api version set
                    string apiversionsetid = apiTemplateResource["properties"].Value<string>("apiVersionSetId");
                    if (!string.IsNullOrEmpty(apiversionsetid))
                    {
                        AzureResourceId aiapiversion = new AzureResourceId(apiInstance["properties"].Value<string>("apiVersionSetId"));

                        var versionsetResource = template.AddVersionSet(await resourceCollector.GetResource(apiversionsetid));
                        if (versionsetResource != null)
                        {
                            string resourceid = $"[resourceId('Microsoft.ApiManagement/service/api-version-sets',{versionsetResource.GetResourceId()})]";
                            apiTemplateResource["properties"]["apiVersionSetId"] = resourceid;
                            apiTemplateResource.Value<JArray>("dependsOn").Add(resourceid);
                        }
                    }
                    string openidProviderId = GetOpenIdProviderId(apiTemplateResource);
                    if (!String.IsNullOrWhiteSpace(openidProviderId))
                    {
                        if (this.openidConnectProviders == null)
                        {
                            openidConnectProviders = new List<JObject>();
                            var providers = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/openidConnectProviders");
                            foreach (JObject openidConnectProvider in (providers == null ? new JArray() : providers.Value<JArray>("value")))
                            {
                                openidConnectProviders.Add(openidConnectProvider);
                            }
                        }
                        var openIdProvider = this.openidConnectProviders.FirstOrDefault(x => x.Value<string>("name") == openidProviderId);
                        template.CreateOpenIDConnectProvider(openIdProvider, true);
                    }

                    var operations = await resourceCollector.GetResource(id + "/operations");
                    foreach (JObject operation in (operations == null
                        ? new JArray()
                        : operations.Value<JArray>("value")))
                    {
                        var opId = operation.Value<string>("id");

                        var operationInstance = await resourceCollector.GetResource(opId);
                        var operationTemplateResource = template.CreateOperation(operationInstance);
                        if (!exportSwaggerDefinition)
                        {
                            apiTemplateResource.Value<JArray>("resources").Add(operationTemplateResource);
                        }
                        var operationPolicies = await resourceCollector.GetResource(opId + "/policies");
                        foreach (JObject policy in (operationPolicies == null
                            ? new JArray()
                            : operationPolicies.Value<JArray>("value")))
                        {
                            var pol = template.CreatePolicy(policy);

                            //add properties
                            this.PolicyHandleProperties(pol, apiTemplateResource.Value<string>("name"),
                                (operationInstance.Value<string>("name").StartsWith("api-")
                                    ? operationInstance.Value<string>("name").Substring(4,
                                        (operationInstance.Value<string>("name")
                                            .LastIndexOf("-" + operationInstance["properties"]
                                                             .Value<string>("method").ToLower())) - 4)
                                    : operationInstance.Value<string>("name")));

                            var operationSuffix = apiInstance.Value<string>("name") + "_" +
                                                  operationInstance.Value<string>("name");
                            //Handle Azure Resources
                            if (!this.PolicyHandeAzureResources(pol, apiTemplateResource.Value<string>("name"),
                                template))
                            {
                                PolicyHandeBackendUrl(pol, operationSuffix, template);
                            }

                            var backendid =
                                TemplateHelper.GetBackendIdFromnPolicy(policy["properties"]
                                    .Value<string>("policyContent"));

                            if (!string.IsNullOrEmpty(backendid))
                            {
                                JObject backendInstance = await HandleBackend(template, operationSuffix, backendid);

                                if (apiTemplateResource.Value<JArray>("dependsOn") == null)
                                    apiTemplateResource["dependsOn"] = new JArray();

                                //add dependeOn
                                apiTemplateResource.Value<JArray>("dependsOn").Add(
                                    $"[resourceId('Microsoft.ApiManagement/service/backends', parameters('{GetServiceName(servicename)}'), '{backendInstance.Value<string>("name")}')]");
                            }
                            await AddCertificate(policy, template);

                            if(exportSwaggerDefinition)
                                apiTemplateResource.Value<JArray>("resources").Add(pol);
                            else
                                operationTemplateResource.Value<JArray>("resources").Add(pol);
                            //handle nextlink?
                        }
                        //handle nextlink?                
                    }
                    if(exportSwaggerDefinition)
                    {
                        apiTemplateResource["properties"]["contentFormat"] = "swagger-json";
                        var swaggerExport = await resourceCollector.GetResource(id + "?format=swagger-link&export=true", apiversion: "2018-06-01-preview");
                        var swaggerUrl = swaggerExport.Value<string>("link");
                        var swaggerContent = await resourceCollector.GetResourceByURL(swaggerUrl);
                        var serviceUrl = apiInstance["properties"].Value<string>("serviceUrl");
                        if(!String.IsNullOrWhiteSpace(serviceUrl))
                        {
                            var serviceUri = new Uri(serviceUrl);
                            swaggerContent["host"] = serviceUri.Host;
                            swaggerContent["basePath"] = serviceUri.AbsolutePath;
                            swaggerContent["schemes"] = JArray.FromObject(new[] {serviceUri.Scheme});
                        }
                        apiTemplateResource["properties"]["contentValue"] = swaggerContent.ToString();
                    }

                    var apiPolicies = await resourceCollector.GetResource(id + "/policies");
                    foreach (JObject policy in (apiPolicies == null ? new JArray() : apiPolicies.Value<JArray>("value")))
                    {
                        //Handle SOAP Backend
                        var backendid = TemplateHelper.GetBackendIdFromnPolicy(policy["properties"].Value<string>("policyContent"));
                        await AddCertificate(policy, template);
                        PolicyHandeBackendUrl(policy, apiInstance.Value<string>("name"), template);
                        var policyTemplateResource = template.CreatePolicy(policy);
                        this.PolicyHandleProperties(policy, apiTemplateResource.Value<string>("name"), null);
                        apiTemplateResource.Value<JArray>("resources").Add(policyTemplateResource);


                        if (!string.IsNullOrEmpty(backendid))
                        {
                            JObject backendInstance = await HandleBackend(template, apiObject.Value<string>("name"), backendid);

                            if (apiTemplateResource.Value<JArray>("dependsOn") == null)
                                apiTemplateResource["dependsOn"] = new JArray();

                            //add dependeOn
                            apiTemplateResource.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/backends', parameters('{GetServiceName(servicename)}'), '{backendInstance.Value<string>("name")}')]");
                        }

                        //handle nextlink?
                    }
                    //schemas
                    if (!exportSwaggerDefinition)
                    {
                        var apiSchemas = await resourceCollector.GetResource(id + "/schemas");
                        foreach (JObject schema in (apiSchemas == null ? new JArray() : apiSchemas.Value<JArray>("value")))
                        {
                            var schemaTemplate = template.CreateAPISchema(schema);
                            apiTemplateResource.Value<JArray>("resources").Add(JObject.FromObject(schemaTemplate));
                        }
                    }
                    //handle nextlink?
                }
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
                    var productApis = await resourceCollector.GetResource(id + "/apis", (string.IsNullOrEmpty(apiFilters) ? "" : $"$filter={apiFilters}"));

                    // Skip product if not related to an API in the filter.
                    if (productApis != null && productApis.Value<JArray>("value").Count > 0)
                    {
                        var productTemplateResource = template.AddProduct(productObject);

                        foreach (JObject productApi in (productApis == null ? new JArray() : productApis.Value<JArray>("value")))
                        {
                            var productProperties = productApi["properties"];
                            if (productProperties["apiVersionSetId"] != null)
                            {
                                var apiVersionSetId = new AzureResourceId(productProperties["apiVersionSetId"].ToString()).ValueAfter("api-version-sets");
                                productProperties["apiVersionSetId"] = $"[resourceId('Microsoft.ApiManagement/service/api-version-sets', parameters('{GetServiceName(servicename)}'), '{apiVersionSetId}')]";
                            }
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
                            }
                            productTemplateResource.Value<JArray>("resources").Add(template.AddProductSubObject(group));
                            productTemplateResource.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/groups', parameters('{GetServiceName(servicename)}'), '{group.Value<string>("name")}')]");
                        }
                        var policies = await resourceCollector.GetResource(id + "/policies");
                        foreach (JObject policy in (policies == null ? new JArray() : policies.Value<JArray>("value")))
                        {
                            productTemplateResource.Value<JArray>("resources").Add(template.AddProductSubObject(policy));
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
                if (identifiedProperty == null)
                {
                    identifiedProperty = identifiedProperties.FirstOrDefault(idp => name == $"{idp.name}-key" && idp.type == Property.PropertyType.Function);
                }
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
                    else if (identifiedProperty.type == Property.PropertyType.Function)
                    {
                        //    "replacewithfunctionoperationname"
                        propertyObject["properties"]["value"] = $"[{identifiedProperty.extraInfo.Replace("replacewithfunctionoperationname", $"{identifiedProperty.operationName}")}]";
                    }
                    var propertyTemplate = template.AddProperty(propertyObject);

                    if (!parametrizePropertiesOnly)
                    {
                        string resourceid = $"[resourceId('Microsoft.ApiManagement/service/properties',{propertyTemplate.GetResourceId()})]";
                        foreach (var apiName in identifiedProperty.apis)
                        {
                            var apiTemplate = template.resources.Where(rr => rr.Value<string>("name") == apiName).FirstOrDefault();
                            if (apiTemplate != null)
                                apiTemplate.Value<JArray>("dependsOn").Add(resourceid);
                        }
                    }
                }
            }

            return JObject.FromObject(template);

        }

        private async Task<JObject> AddServiceResource(JObject apimTemplateResource, string resourceName, Func<JObject, JObject> createResource)
        {
            var resources = await resourceCollector.GetResource(GetAPIMResourceIDString() + resourceName);
            foreach (JObject resource in (resources == null ? new JArray() : resources.Value<JArray>("value")))
            {
                var newResource = createResource(resource);
                apimTemplateResource.Value<JArray>("resources").Add(newResource);
            }
            return resources;
        }

        private string GetServiceName(string serviceName)
        {
            var template = new DeploymentTemplate(this.parametrizePropertiesOnly, fixedServiceNameParameter);
            return template.GetServiceName(serviceName);
        }

        private async Task AddCertificate(JObject policy, DeploymentTemplate template)
        {
            var certificateThumbprint = TemplateHelper.GetCertificateThumbPrintIdFromPolicy(policy["properties"].Value<string>("policyContent"));
            if (!string.IsNullOrEmpty(certificateThumbprint))
            {
                var certificates = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/certificates");
                if (certificates != null)
                {
                    var certificate = certificates.Value<JArray>("value").FirstOrDefault(x =>
                        x?["properties"]?.Value<string>("thumbprint") == certificateThumbprint);
                    if (certificate != null)
                        template.CreateCertificate(JObject.FromObject(certificate), true);
                }
            }
        }

        private string GetOpenIdProviderId(JObject apiTemplateResource)
        {
            if (apiTemplateResource == null || !apiTemplateResource["properties"].HasValues ||
                !apiTemplateResource["properties"]["authenticationSettings"].HasValues ||
                !apiTemplateResource["properties"]["authenticationSettings"]["openid"].HasValues ||
                apiTemplateResource["properties"]["authenticationSettings"]["openid"]["openidProviderId"] == null)
                return string.Empty;
            JToken openIdProviderId = apiTemplateResource?["properties"]["authenticationSettings"]["openid"]["openidProviderId"];
            return openIdProviderId.Value<string>();
        }

        private async Task<JObject> HandleBackend(DeploymentTemplate template, string startname, string backendid)
        {
            var backendInstance = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/backends/" + backendid);
            JObject azureResource = null;
            if (backendInstance["properties"]["resourceId"] != null)
            {
                string version = "2018-02-01";
                if (backendInstance["properties"].Value<string>("resourceId").Contains("Microsoft.Logic"))
                {
                    version = "2017-07-01";
                }

                azureResource = await resourceCollector.GetResource(backendInstance["properties"].Value<string>("resourceId"), "", version);
            }

            var property = template.AddBackend(backendInstance, azureResource);

            if (property != null)
            {
                if (property.type == Property.PropertyType.LogicApp)
                {
                    var idp = this.identifiedProperties.Where(pp => pp.name.StartsWith(startname) && pp.name.Contains("-invoke")).FirstOrDefault();
                    if (idp != null)
                    {
                        idp.extraInfo = property.extraInfo;
                        idp.type = Property.PropertyType.LogicAppRevisionGa;
                    }
                }
                else if (property.type == Property.PropertyType.Function)
                {
                    property.operationName = GetOperationName(startname);
                    identifiedProperties.Add(property);
                    foreach (var idp in this.identifiedProperties.Where(pp => pp.name.ToLower().StartsWith(property.name) && !pp.name.Contains("-invoke")))
                    {
                        idp.extraInfo = property.extraInfo;
                        idp.type = Property.PropertyType.Function;
                    }
                }
            }

            return backendInstance;
        }

        private static string GetOperationName(string startname)
        {
            if (startname.IndexOf("_") >= 0)
                return startname.Split('_')[1].Replace("-", String.Empty);
            return startname;
        }


        public void PolicyHandleProperties(JObject policy, string apiname, string operationName)
        {
            var policyContent = policy["properties"].Value<string>("policyContent");
            HandleProperties(apiname, operationName, policyContent);
        }

        private void HandleProperties(string apiname, string operationName, string content)
        {
            var match = Regex.Match(content, "{{(?<name>[-_.a-zA-Z0-9]*)}}");

            while (match.Success)
            {
                string name = match.Groups["name"].Value;
                var idp = identifiedProperties.Where(pp => pp.name == name).FirstOrDefault();
                if (idp == null)
                {
                    this.identifiedProperties.Add(new Property()
                    {
                        name = name,
                        type = Property.PropertyType.Standard,
                        apis = new List<string>(new string[] { apiname }),
                        operationName = operationName
                    });
                }
                else if (!idp.apis.Contains(apiname))
                {
                    idp.apis.Add(apiname);
                }
                match = match.NextMatch();
            }
        }

        public List<Property> identifiedProperties = new List<Property>();
        public List<JObject> openidConnectProviders = null;

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

                if (backendService.Attribute("base-url") != null && !backendService.Attribute("base-url").Value.Contains("{{"))
                {
                    string baseUrl = backendService.Attribute("base-url").Value;
                    var paramname = template.AddParameter($"api_{apiname}_backendurl", "string", baseUrl);
                    if (replaceSetBackendServiceBaseUrlAsProperty)
                    {
                        policy["properties"]["policyContent"] = CreatePolicyContentReplaceBaseUrlWithProperty(backendService, policyContent, paramname);
                        string id = GetIdFromPolicy(policy);
                        AzureResourceId resourceId = new AzureResourceId(id);
                        var lookFor = $"/service/{resourceId.ValueAfter("service")}";
                        var index = id.IndexOf(lookFor);
                        var serviceId = id.Substring(0, index + lookFor.Length);
                        var property = new
                        {
                            id = $"{serviceId}/properties/{paramname}",
                            type = "Microsoft.ApiManagement/service/properties",
                            name = paramname,
                            properties = new
                            {
                                displayName = paramname,
                                value = $"[parameters('{paramname}')]",
                                secret = false
                            }
                        };
                        template.AddProperty(JObject.FromObject(property));
                    }
                    else
                    {
                        policy["properties"]["policyContent"] = CreatePolicyContentReplaceBaseUrl(backendService, policyContent, $"parameters('{paramname}')");
                    }
                }

            }

        }

        private static string GetIdFromPolicy(JObject policy)
        {
            var id = policy.Value<string>("id");
            if (id != null)
                return id;
            var comment = policy.Value<string>("comments");
            return comment.Substring(comment.IndexOf("/subscriptions/", StringComparison.InvariantCulture));
        }


        private string CreatePolicyContentReplaceBaseUrl(XElement backendService, string policyContent, string replaceText)
        {
            var baseUrl = backendService.Attribute("base-url");
            if (baseUrl != null && policyContent.IndexOf(baseUrl.Value) > -1)
            {
                int index = policyContent.IndexOf(baseUrl.Value);
                return "[Concat('" + policyContent.Substring(0, index).Replace("'", "''") + "'," + replaceText + ",'" + policyContent.Substring(index + baseUrl.Value.Length).Replace("'", "''") + "')]";
            }
            return policyContent;
        }
        private string CreatePolicyContentReplaceBaseUrlWithProperty(XElement backendService, string policyContent, string parameterName)
        {
            var baseUrl = backendService.Attribute("base-url");
            if (baseUrl != null && policyContent.IndexOf(baseUrl.Value) > -1)
            {
                return policyContent.Replace(baseUrl.Value, $"{{{{{parameterName}}}}}");
            }
            return policyContent;
        }

    }



}
