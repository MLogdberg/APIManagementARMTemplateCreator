using APIManagementTemplate.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

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
        private bool exportCertificates;
        private bool exportProducts;
        private bool exportTags;
        private bool parametrizePropertiesOnly;
        private bool replaceSetBackendServiceBaseUrlAsProperty;
        private bool fixedServiceNameParameter;
        private bool fixedKeyVaultNameParameter;
        private readonly bool exportBackendInstances;
        private bool createApplicationInsightsInstance;
        private string apiVersion;
        private readonly bool parameterizeBackendFunctionKey;
        private readonly bool exportSwaggerDefinition;
        IResourceCollector resourceCollector;
        private string separatePolicyOutputFolder;
        private bool chainDependencies;
        private bool exportApiPropertiesAndBackend;
        private readonly string[] ignoreProperties;


        public TemplateGenerator(string servicename, string subscriptionId, string resourceGroup, string apiFilters,
            bool exportGroups, bool exportProducts, bool exportPIManagementInstance, bool parametrizePropertiesOnly,
            IResourceCollector resourceCollector, bool replaceSetBackendServiceBaseUrlAsProperty = false,
            bool fixedServiceNameParameter = false, bool createApplicationInsightsInstance = false,
            string apiVersion = null, bool parameterizeBackendFunctionKey = false, bool exportSwaggerDefinition = false,
            bool exportCertificates = true, bool exportTags = false, string separatePolicyOutputFolder = "",
            bool chainDependencies = false, bool exportApiPropertiesAndBackend = true,
            bool fixedKeyVaultNameParameter = false, bool exportBackendInstances = true, string[] ignoreProperties = null)
        {
            this.servicename = servicename;
            this.subscriptionId = subscriptionId;
            this.resourceGroup = resourceGroup;
            this.apiFilters = apiFilters ?? "";
            this.exportCertificates = exportCertificates;
            this.exportGroups = exportGroups;
            this.exportProducts = exportProducts;
            this.exportTags = exportTags;
            this.exportPIManagementInstance = exportPIManagementInstance;
            this.parametrizePropertiesOnly = parametrizePropertiesOnly;
            this.replaceSetBackendServiceBaseUrlAsProperty = replaceSetBackendServiceBaseUrlAsProperty;
            this.resourceCollector = resourceCollector;
            this.fixedServiceNameParameter = fixedServiceNameParameter;
            this.fixedKeyVaultNameParameter = fixedKeyVaultNameParameter;
            this.exportBackendInstances = exportBackendInstances;
            this.createApplicationInsightsInstance = createApplicationInsightsInstance;
            this.apiVersion = apiVersion;
            this.parameterizeBackendFunctionKey = parameterizeBackendFunctionKey;
            this.exportSwaggerDefinition = exportSwaggerDefinition;
            this.separatePolicyOutputFolder = separatePolicyOutputFolder;
            this.chainDependencies = chainDependencies;
            this.exportApiPropertiesAndBackend = exportApiPropertiesAndBackend;
            this.ignoreProperties = ignoreProperties;
        }

        private string GetAPIMResourceIDString()
        {
            return $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.ApiManagement/service/{servicename}";
        }

        public async Task<JObject> GenerateTemplate()
        {
            DeploymentTemplate template = new DeploymentTemplate(this.parametrizePropertiesOnly, this.fixedServiceNameParameter, this.createApplicationInsightsInstance, this.parameterizeBackendFunctionKey, this.separatePolicyOutputFolder, this.chainDependencies, this.fixedKeyVaultNameParameter);
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

                if (this.exportTags)
                    await AddServiceResource(apimTemplateResource, "/tags",
                    tags => template.CreateTags(tags, false));
            }
            //check for special productname filter
            var getProductname = Regex.Match(apiFilters, "(?<=productname\\s*eq\\s*\\')(.+?)(?=\\')", RegexOptions.IgnoreCase);
            if (getProductname.Success)
            {
                apiFilters = Regex.Replace(apiFilters, "productname\\s*eq\\s*\'(.+?)(\')", "");

                var apiFilterList = new List<string>();
                var productApis = await resourceCollector.GetResource(GetAPIMResourceIDString() + $"/products/{getProductname.Value}/apis");
                foreach (JObject api in productApis["value"])
                {
                    apiFilterList.Add($"name eq '{api["name"]}'");
                }

                apiFilters = "(" + string.Join(" or ", apiFilterList) + ")" + apiFilters;
            }


            var apiObjectResult = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/apis", (string.IsNullOrEmpty(apiFilters) ? "" : $"$filter={apiFilters}"));
            IEnumerable<JToken> apis = new List<JToken>();
            if (apiObjectResult != null)
            {
                apis = (!string.IsNullOrEmpty(apiVersion) ? apiObjectResult.Value<JArray>("value").Where(aa => aa["properties"].Value<string>("apiVersion") == this.apiVersion) : apiObjectResult.Value<JArray>("value"));
                foreach (JObject apiObject in apis)
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
                            string resourceid = $"[resourceId('Microsoft.ApiManagement/service/apiVersionSets',{versionsetResource.GetResourceId()})]";
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
                    string previousOperationName = null;
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
                                GetOperationName(operationInstance));

                            var operationSuffix = apiInstance.Value<string>("name") + "_" +
                                                  operationInstance.Value<string>("name");
                            //Handle Azure Resources
                            if (!this.PolicyHandeAzureResources(pol, apiTemplateResource.Value<string>("name"),
                                template))
                            {
                                PolicyHandeBackendUrl(pol, operationSuffix, template);
                            }

                            var policyPropertyName = policy["properties"].Value<string>("policyContent") == null ? "value" : "policyContent";
                            string policyContent = policy["properties"].Value<string>(policyPropertyName);

                            var backendid = TemplateHelper.GetBackendIdFromnPolicy(policyContent);

                            if (!string.IsNullOrEmpty(backendid) && exportBackendInstances)
                            {
                                BackendObject bo = await HandleBackend(template, operationSuffix, backendid);
                                JObject backendInstance = bo?.backendInstance;
                                if (backendInstance != null)
                                {
                                    if (apiTemplateResource.Value<JArray>("dependsOn") == null)
                                        apiTemplateResource["dependsOn"] = new JArray();

                                    //add dependeOn
                                    apiTemplateResource.Value<JArray>("dependsOn").Add(
                                        $"[resourceId('Microsoft.ApiManagement/service/backends', parameters('{GetServiceName(servicename)}'), '{backendInstance.Value<string>("name")}')]");
                                }
                                if (bo?.backendProperty != null)
                                {
                                    if (bo.backendProperty.type == Property.PropertyType.LogicApp)
                                    {
                                        var urltemplatestring = TemplateHelper.GetAPIMGenereatedRewritePolicyTemplate(policyContent);
                                        var match = Regex.Match(urltemplatestring, "{{(?<name>[-_.a-zA-Z0-9]*)}}");
                                        if (match.Success)
                                        {
                                            string name = match.Groups["name"].Value;
                                            var idp = identifiedProperties.Where(pp => pp.name == name).FirstOrDefault();
                                            if (idp != null)
                                            {
                                                idp.extraInfo = bo.backendProperty.extraInfo;
                                                idp.type = Property.PropertyType.LogicAppRevisionGa;
                                            }
                                        }
                                    }
                                }
                            }
                            if (exportCertificates) await AddCertificate(policy, template);

                            if (Directory.Exists(separatePolicyOutputFolder))
                            {
                                pol = ReplacePolicyWithFileLink(template, pol, operationSuffix);
                            }

                            if (exportSwaggerDefinition)
                                apiTemplateResource.Value<JArray>("resources").Add(pol);
                            else
                                operationTemplateResource.Value<JArray>("resources").Add(pol);

                            //all other fixed let's add the not found properties


                            //handle nextlink?
                        }
                        //handle nextlink?               

                        //add dependency to make sure not all operations are deployed at the same time. This results in timeouts when having a lot of operations
                        if (previousOperationName != null)
                        {
                            //operationTemplateResource.Value<JArray>("dependsOn").Where(aa => aa.ToString().Contains("'Microsoft.ApiManagement/service/apis'")).First();
                            string apiname = parametrizePropertiesOnly ? $"'{apiInstance.Value<string>("name")}'" : $"parameters('api_{apiInstance.Value<string>("name")}_name')";
                            var dep = $"[resourceId('Microsoft.ApiManagement/service/apis/operations', parameters('{GetServiceName(servicename)}'), {apiname}, '{previousOperationName}')]";
                            operationTemplateResource.Value<JArray>("dependsOn").Add(dep);
                        }
                        previousOperationName = operationInstance.Value<string>("name");
                    }
                    if (exportSwaggerDefinition)
                    {
                        apiTemplateResource["properties"]["contentFormat"] = "swagger-json";
                        var swaggerExport = await resourceCollector.GetResource(id + "?format=swagger-link&export=true", apiversion: "2019-01-01");
                        var swaggerUrl = swaggerExport.Value<string>("link");
                        // On my system, the link is in a value object. Not sure if this is changed in APIM. Therefore keep the old assigment and check for null.
                        if (swaggerUrl == null)
                        {
                            swaggerUrl = swaggerExport["value"].Value<string>("link");
                        }
                        var swaggerContent = await resourceCollector.GetResourceByURL(swaggerUrl);
                        var serviceUrl = apiInstance["properties"].Value<string>("serviceUrl");
                        if (!String.IsNullOrWhiteSpace(serviceUrl))
                        {
                            var serviceUri = new Uri(serviceUrl);
                            swaggerContent["host"] = serviceUri.Host;
                            swaggerContent["basePath"] = serviceUri.AbsolutePath;
                            swaggerContent["schemes"] = JArray.FromObject(new[] { serviceUri.Scheme });
                        }
                        apiTemplateResource["properties"]["contentValue"] = swaggerContent.ToString();
                    }

                    if (exportApiPropertiesAndBackend)
                    {
                        var apiPolicies = await resourceCollector.GetResource(id + "/policies");
                        foreach (JObject policy in (apiPolicies == null ? new JArray() : apiPolicies.Value<JArray>("value")))
                        {
                            //Handle SOAP Backend
                            var policyPropertyName = policy["properties"].Value<string>("policyContent") == null ? "value" : "policyContent";
                            var backendid = TemplateHelper.GetBackendIdFromnPolicy(policy["properties"].Value<string>(policyPropertyName));

                            if (exportCertificates) await AddCertificate(policy, template);
                            PolicyHandeBackendUrl(policy, apiInstance.Value<string>("name"), template);
                            var policyTemplateResource = template.CreatePolicy(policy);
                            this.PolicyHandleProperties(policy, apiTemplateResource.Value<string>("name"), null);
                            apiTemplateResource.Value<JArray>("resources").Add(policyTemplateResource);


                            if (!string.IsNullOrEmpty(backendid) && exportBackendInstances)
                            {
                                var bo = await HandleBackend(template, apiObject.Value<string>("name"), backendid);
                                JObject backendInstance = bo.backendInstance;
                                if (backendInstance != null)
                                {
                                    if (apiTemplateResource.Value<JArray>("dependsOn") == null)
                                        apiTemplateResource["dependsOn"] = new JArray();

                                    //add dependeOn
                                    apiTemplateResource.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/backends', parameters('{GetServiceName(servicename)}'), '{backendInstance.Value<string>("name")}')]");
                                }
                            }

                            if (Directory.Exists(separatePolicyOutputFolder))
                            {
                                ReplacePolicyWithFileLink(template, policyTemplateResource, apiInstance.Value<string>("name") + "_AllOperations");
                            }

                            //handle nextlink?
                        }
                    }
                    if (!exportSwaggerDefinition)
                    {
                        var apiSchemas = await resourceCollector.GetResource(id + "/schemas");
                        foreach (JObject schema in (apiSchemas == null ? new JArray() : apiSchemas.Value<JArray>("value")))
                        {
                            var schemaTemplate = template.CreateAPISchema(schema);
                            apiTemplateResource.Value<JArray>("resources").Add(JObject.FromObject(schemaTemplate));
                        }
                    }

                    //diagnostics
                    var loggers = resourceCollector.GetResource(GetAPIMResourceIDString() + "/loggers").Result;
                    var logger = loggers == null ? new JArray() : loggers.Value<JArray>("value");
                    var diagnostics = await resourceCollector.GetResource(id + "/diagnostics", apiversion: "2019-01-01");
                    foreach (JObject diagnostic in diagnostics.Value<JArray>("value"))
                    {
                        if (diagnostic.Value<string>("type") == "Microsoft.ApiManagement/service/apis/diagnostics")
                        {
                            var diagnosticTemplateResource = template.CreateApiDiagnostic(diagnostic, logger, false);
                            apiTemplateResource.Value<JArray>("resources").Add(diagnosticTemplateResource);
                        }
                    }

                    //tags
                    if (this.exportTags)
                    {
                        var apiTags = await resourceCollector.GetResource(id + "/tags");
                        foreach (JObject tag in (apiTags == null ? new JArray() : apiTags.Value<JArray>("value")))
                        {
                            var tagTemplate = template.CreateAPITag(tag);
                            apiTemplateResource.Value<JArray>("resources").Add(JObject.FromObject(tagTemplate));
                            //shoudl we get the root tag instead of copying underlaying tags?
                            if (!exportPIManagementInstance)
                            {
                                tagTemplate.RemoveNameAt(1);
                                tagTemplate.type = "Microsoft.ApiManagement/service/tags";
                                tagTemplate.dependsOn.RemoveAll();
                                template.resources.Add(JObject.FromObject(tagTemplate));
                                apiTemplateResource.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/tags', {tagTemplate.GetResourceId()})]");
                            }
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
                        //skip product when filter by productname and not this product
                        if (getProductname.Success && !getProductname.Value.Equals(productObject.Value<string>("name"), StringComparison.OrdinalIgnoreCase))
                            continue;

                        var productTemplateResource = template.AddProduct(productObject);

                        var listOfApiNamesInThisSearch = apis.Select(api => api.Value<string>("name")).ToList();

                        foreach (JObject productApi in (productApis == null ? new JArray() : productApis.Value<JArray>("value")))
                        {
                            //only take the api's inside the api query
                            if (!listOfApiNamesInThisSearch.Contains(productApi.Value<string>("name")))
                            {
                                continue;
                            }

                            var productProperties = productApi["properties"];
                            if (productProperties["apiVersionSetId"] != null)
                            {
                                var apiVersionSetId = new AzureResourceId(productProperties["apiVersionSetId"].ToString()).ValueAfter("apiVersionSets");
                                productProperties["apiVersionSetId"] = $"[resourceId('Microsoft.ApiManagement/service/apiVersionSets', parameters('{GetServiceName(servicename)}'), '{apiVersionSetId}')]";
                            }
                            productTemplateResource.Value<JArray>("resources").Add(template.AddProductSubObject(productApi));

                            //also add depends On for API
                            string apiname = parametrizePropertiesOnly ? $"'{productApi.Value<string>("name")}'" : $"parameters('api_{productApi.Value<string>("name")}_name')";
                            productTemplateResource.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('{GetServiceName(servicename)}'), {apiname})]");
                        }

                        if (exportGroups)
                        {
                            var groups = await resourceCollector.GetResource(id + "/groups");
                            foreach (JObject group in (groups == null ? new JArray() : groups.Value<JArray>("value")))
                            {
                                if (group["properties"].Value<bool>("builtIn") == false)
                                {
                                    // Add group resource
                                    var groupObject = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/groups/" + group.Value<string>("name"));
                                    template.AddGroup(groupObject);
                                    productTemplateResource.Value<JArray>("dependsOn").Add($"[resourceId('Microsoft.ApiManagement/service/groups', parameters('{GetServiceName(servicename)}'), parameters('{template.AddParameter($"group_{group.Value<string>("name")}_name", "string", group.Value<string>("name"))}'))]");
                                }
                                productTemplateResource.Value<JArray>("resources").Add(template.AddProductSubObject(group));
                            }
                        }
                        var policies = await resourceCollector.GetResource(id + "/policies");
                        foreach (JObject policy in (policies == null ? new JArray() : policies.Value<JArray>("value")))
                        {
                            JObject pol = template.AddProductSubObject(policy);

                            if (Directory.Exists(separatePolicyOutputFolder))
                            {
                                pol = ReplacePolicyWithFileLink(template, pol, productObject.Value<string>("name"));
                            }

                            productTemplateResource.Value<JArray>("resources").Add(pol);
                            this.PolicyHandleProperties(policy, productTemplateResource.Value<string>("name"), null);
                        }
                    }
                }
            }

            var properties = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/namedValues", apiversion: "2020-06-01-preview");

            //  var properties = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/properties",apiversion: "2020-06-01-preview");
            //has more?
            foreach (JObject propertyObject in (properties == null ? new JArray() : properties.Value<JArray>("value")))
            {

                var id = propertyObject.Value<string>("id");
                var displayName = propertyObject["properties"].Value<string>("displayName");
                if (ignoreProperties != null && ignoreProperties.Contains(displayName))
                {
                    continue;
                }

                var identifiedProperty = this.identifiedProperties.Where(idp => displayName.EndsWith(idp.name)).FirstOrDefault();
                if (identifiedProperty == null)
                {
                    identifiedProperty = identifiedProperties.FirstOrDefault(idp => displayName == $"{idp.name}-key" && idp.type == Property.PropertyType.Function);
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
                        propertyObject["properties"]["value"] = $"[{identifiedProperty.extraInfo}]";
                    }


                    var propertyTemplate = template.AddNamedValues(propertyObject);

                    if (!parametrizePropertiesOnly)
                    {
                        string resourceid = $"[resourceId('Microsoft.ApiManagement/service/namedValues',{propertyTemplate.GetResourceId()})]";
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

        internal static string GetOperationName(JObject operationInstance)
        {
            var operationName = operationInstance.Value<string>("name");
            var method = operationInstance["properties"].Value<string>("method").ToLower();
            if (!operationName.StartsWith("api-"))
                return operationName;
            var length = (operationName.ToLower().LastIndexOf("-" + method)) - 4;
            return length > 0 ? operationName.Substring(4, length) : operationName;
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
            var policyPropertyName = policy["properties"].Value<string>("policyContent") == null ? "value" : "policyContent";
            var certificateThumbprint = TemplateHelper.GetCertificateThumbPrintIdFromPolicy(policy["properties"].Value<string>(policyPropertyName));
            if (!string.IsNullOrEmpty(certificateThumbprint))
            {
                var certificates = await resourceCollector.GetResource(GetAPIMResourceIDString() + "/certificates");
                if (certificates != null)
                {
                    // If the thumbprint is a property, we must lookup the value of the property first.
                    var match = Regex.Match(certificateThumbprint, "{{(?<name>[-_.a-zA-Z0-9]*)}}");

                    if (match.Success)
                    {
                        string propertyName = match.Groups["name"].Value;
                        var propertyResource = await resourceCollector.GetResource(GetAPIMResourceIDString() + $"/properties/{propertyName}");
                        if (propertyResource != null)
                            certificateThumbprint = propertyResource["properties"].Value<string>("value");
                    }

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


        public class BackendObject
        {
            public JObject backendInstance { get; set; }
            public Property backendProperty { get; set; }
        }
        private async Task<BackendObject> HandleBackend(DeploymentTemplate template, string startname, string backendid)
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

            //sometime old endpoint are not cleaned-up, this will result in null. So skip these resources
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
                    // old way of handling, removed 2019-11-03
                    //property.operationName = GetOperationName(startname);
                    property.operationName = startname;
                    identifiedProperties.Add(property);
                    foreach (var idp in this.identifiedProperties.Where(pp => pp.name.ToLower().StartsWith(property.name) && !pp.name.Contains("-invoke")))
                    {
                        idp.extraInfo = property.extraInfo;
                        idp.type = Property.PropertyType.Function;
                    }
                }
            }

            return new BackendObject() { backendInstance = backendInstance, backendProperty = property };
        }

        private static string GetOperationName(string startname)
        {
            if (startname.IndexOf("_") >= 0)
                return startname.Split('_')[1].Replace("-", String.Empty);
            return startname;
        }


        public void PolicyHandleProperties(JObject policy, string apiname, string operationName)
        {
            var policyPropertyName = policy["properties"].Value<string>("policyContent") == null ? "value" : "policyContent";
            var policyContent = policy["properties"].Value<string>(policyPropertyName);
            HandleProperties(apiname, operationName, policyContent);
        }

        private void HandleProperties(string apiname, string operationName, string content)
        {
            if (content == null)
                return;

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
            var policyPropertyName = policy["properties"].Value<string>("policyContent") == null ? "value" : "policyContent";
            var policyContent = policy["properties"].Value<string>(policyPropertyName);

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
                        policy["properties"][policyPropertyName] = CreatePolicyContentReplaceBaseUrl(backendService, policyContent, $"{listCallbackUrl}.basePath");

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
                    }
                }

            }
            return commentMatch.Success;
        }
        public void PolicyHandeBackendUrl(JObject policy, string apiname, DeploymentTemplate template)
        {
            var policyPropertyName = policy["properties"].Value<string>("policyContent") == null ? "value" : "policyContent";
            var policyContent = policy["properties"].Value<string>(policyPropertyName);

            var policyXMLDoc = XDocument.Parse(policyContent);
            //find the last backend service and add as parameter
            var backendService = policyXMLDoc.Descendants().Where(dd => dd.Name == "set-backend-service").LastOrDefault();
            if (backendService != null)
            {
                // This does not work in all cases. If you want to be sure, use a property as placeholder.
                if (backendService.Attribute("base-url") != null && !backendService.Attribute("base-url").Value.Contains("{{") && !parametrizePropertiesOnly)
                {
                    string baseUrl = backendService.Attribute("base-url").Value;
                    var paramname = template.AddParameter($"api_{apiname}_backendurl", "string", baseUrl);
                    if (replaceSetBackendServiceBaseUrlAsProperty)
                    {
                        policy["properties"][policyPropertyName] = CreatePolicyContentReplaceBaseUrlWithProperty(backendService, policyContent, paramname);

                        string id = GetIdFromPolicy(policy);
                        AzureResourceId resourceId = new AzureResourceId(id);
                        var lookFor = $"/service/{resourceId.ValueAfter("service")}";
                        var index = id.IndexOf(lookFor);
                        var serviceId = id.Substring(0, index + lookFor.Length);
                        var property = new
                        {
                            id = $"{serviceId}/properties/{paramname}",
                            type = "Microsoft.ApiManagement/service/namedValues",
                            name = paramname,
                            properties = new
                            {
                                displayName = paramname,
                                value = $"[parameters('{paramname}')]",
                                secret = false
                            }
                        };
                        template.AddNamedValues(JObject.FromObject(property));
                    }
                    else
                    {
                        policy["properties"][policyPropertyName] = CreatePolicyContentReplaceBaseUrl(backendService, policyContent, $"parameters('{paramname}')");
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

        private JObject ReplacePolicyWithFileLink(DeploymentTemplate template, JObject policy, string policyName)
        {
            var policyPropertyName = policy["properties"].Value<string>("policyContent") == null ? "value" : "policyContent";
            File.WriteAllText(Path.Combine(separatePolicyOutputFolder, $"{policyName}.xml"), policy["properties"].Value<string>(policyPropertyName));
            policy["properties"][policyPropertyName] = $"[concat(parameters('{TemplatesGenerator.TemplatesStorageAccount}'), parameters('{TemplatesGenerator.TemplatesStorageBlobPrefix}'), '/{separatePolicyOutputFolder}/{policyName}.xml', parameters('{TemplatesGenerator.TemplatesStorageAccountSASToken}'))]";
            policyPropertyName = policy["properties"].Value<string>("format") == null ? "contentFormat" : "format";
            policy["properties"][policyPropertyName] = "xml-link";

            // Add repository parameters to the template.
            if (template.parameters[TemplatesGenerator.TemplatesStorageAccount] == null)
            {
                template.parameters[TemplatesGenerator.TemplatesStorageAccount] = JToken.FromObject(new { type = "string", metadata = new { description = "Base URL of the repository" } });
                template.parameters[TemplatesGenerator.TemplatesStorageBlobPrefix] = JToken.FromObject(new { type = "string", defaultValue = String.Empty, metadata = new { description = "Subfolder within container" } });
                template.parameters[TemplatesGenerator.TemplatesStorageAccountSASToken] = JToken.FromObject(new { type = "securestring", defaultValue = String.Empty });
            }

            return policy;
        }
    }
}
