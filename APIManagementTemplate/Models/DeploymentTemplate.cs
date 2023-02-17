using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;

namespace APIManagementTemplate.Models
{
    public class DeploymentTemplate
    {

        [JsonProperty("$schema")]
        public string schema
        {
            get
            {
                return Constants.deploymentSchema;
            }
        }
        public string contentVersion
        {
            get
            {
                return "1.0.0.0";
            }
        }

        public JObject parameters { get; set; }
        public JObject variables { get; set; }

        public IList<JObject> resources { get; set; }

        public JObject outputs { get; set; }

        private bool parametrizePropertiesOnly { get; set; }
        private bool fixedServiceNameParameter { get; set; }
        private bool fixedKeyVaultNameParameter { get; set; }
        private bool referenceApplicationInsightsInstrumentationKey { get; set; }
        private readonly bool parameterizeBackendFunctionKey;
        private string separatePolicyOutputFolder { get; set; }
        private bool chainDependencies { get; set; }
        private string lastProductApi { get; set; }
        private string lastApi { get; set; }

        public DeploymentTemplate(bool parametrizePropertiesOnly = false, bool fixedServiceNameParameter = false, bool referenceApplicationInsightsInstrumentationKey = false, bool parameterizeBackendFunctionKey = false, string separatePolicyOutputFolder = "", bool chainDependencies = false, bool fixedKeyVaultNameParameter = false)
        {
            parameters = new JObject();
            variables = new JObject();
            resources = new List<JObject>();
            outputs = new JObject();

            this.parametrizePropertiesOnly = parametrizePropertiesOnly;
            this.fixedServiceNameParameter = fixedServiceNameParameter;
            this.referenceApplicationInsightsInstrumentationKey = referenceApplicationInsightsInstrumentationKey;
            this.parameterizeBackendFunctionKey = parameterizeBackendFunctionKey;
            this.separatePolicyOutputFolder = separatePolicyOutputFolder;
            this.chainDependencies = chainDependencies;
            this.fixedKeyVaultNameParameter = fixedKeyVaultNameParameter;
        }

        public static DeploymentTemplate FromString(string template)
        {
            return JsonConvert.DeserializeObject<DeploymentTemplate>(template);
        }


        public void RemoveResources_OfType(string type)
        {
            var resources = this.resources.Where(rr => rr.Value<string>("type") == type);
            int count = resources.Count();
            for (int i = 0; i < count; i++)
            {
                RemoveResource(resources.First());
            }
        }


        private void RemoveResource(JObject resource)
        {
            this.parameters.Remove(resource.Value<string>("name").Replace("[parameters('", "").Replace("')]", ""));
            this.resources.Remove(resource);
        }

        public string AddParameter(string paramname, string type, object defaultvalue)
        {
            return AddParameter(paramname, type, new JProperty("defaultValue", defaultvalue));
        }


        public string AddParameter(string paramname, string type, JProperty defaultvalue)
        {
            string realParameterName = paramname;
            JObject param = new JObject();
            param.Add("type", JToken.FromObject(type));
            param.Add(defaultvalue);

            if (this.parameters[paramname] == null)
            {
                this.parameters.Add(paramname, param);
            }
            else
            {
                if (!this.parameters[paramname].Value<string>("defaultValue").Equals(defaultvalue.Value.ToString()))
                {
                    foreach (var p in this.parameters)
                    {
                        if (p.Key.StartsWith(paramname))
                        {
                            for (int i = 2; i < 100; i++)
                            {
                                realParameterName = paramname + i.ToString();
                                if (this.parameters[realParameterName] == null)
                                {
                                    this.parameters.Add(realParameterName, param);
                                    return realParameterName;
                                }
                            }
                        }
                    }
                }
            }
            return realParameterName;
        }

        public string AddVariable(string variablename, string value)
        {
            string realVariableName = variablename;

            if (this.variables[variablename] == null)
            {
                this.variables.Add(variablename, value);
            }
            else
            {
                foreach (var p in this.variables)
                {
                    if (p.Key.StartsWith(variablename))
                    {
                        for (int i = 2; i < 100; i++)
                        {
                            realVariableName = variablename + i.ToString();
                            if (this.variables[realVariableName] == null)
                            {
                                this.variables.Add(realVariableName, value);
                                return realVariableName;
                            }
                        }
                    }
                }
            }
            return realVariableName;
        }


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string WrapParameterName(string paramname, bool isNullValue = false, bool brackets = true)
        {
            if (isNullValue)
            {
                return $"{(brackets ? "[" : String.Empty)}if(empty(parameters('{paramname}')), json('null'), parameters('{paramname}')){(brackets ? "]" : String.Empty)}";
            }
            else
            {
                return $"{(brackets ? "[" : String.Empty)}parameters('" + paramname + $"'){(brackets ? "]" : String.Empty)}";
            }
        }
        public string RemoveWrapParameter(string parameterstring)
        {
            return parameterstring.Replace("[parameters('", "").Replace("')]", "");
        }

        private string GetParameterName(string name, string ending)
        {
            if (!name.StartsWith("[parameters('"))
                return name;

            var tmpname = RemoveWrapParameter(name);
            if (!string.IsNullOrEmpty(ending) && tmpname.Contains(ending))
            {
                tmpname = tmpname.Substring(0, tmpname.IndexOf("_name"));
            }
            return tmpname;
        }

        public void AddParameterFromObject(JObject obj, string propertyName, string propertyType, string paramNamePrefix = "")
        {
            string propValue = propertyType == "secureobject" || propertyType == "object" ? obj[propertyName].ToString() : (string)obj[propertyName];
            if (propValue == null || (propValue.StartsWith("[") && propValue.EndsWith("]")))
                return;
            var defaultValue = propertyType == "secureobject" ? new JObject() : propertyType == "object" ? JObject.Parse(propValue) : obj[propertyName];
            obj[propertyName] = WrapParameterName(this.AddParameter(paramNamePrefix + "_" + propertyName, propertyType, defaultValue));
        }

        /**
         * 
         *  API Management pecifics
         * 
         */

        private bool APIMInstanceAdded = false;
        private string apimservicename;

        public JObject AddAPIManagementInstance(JObject restObject)
        {
            if (restObject == null)
                return null;

            string servicename = restObject.Value<string>("name").ToLowerInvariant();
            string type = restObject.Value<string>("type");
            apimservicename = servicename;
            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = WrapParameterName(AddParameter($"{GetServiceName(servicename)}", "string", servicename));
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["sku"] = restObject["sku"];
            resource["sku"]["name"] = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_sku_name", "string", restObject["sku"].Value<string>("name")));
            resource["sku"]["capacity"] = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_sku_capacity", "string", restObject["sku"].Value<string>("capacity")));
            resource["location"] = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_location", "string", restObject.Value<string>("location")));
            if (restObject["identity"] != null && restObject["identity"].HasValues && restObject["identity"]["type"] != null)
            {
                resource["identity"] = new JObject();
                resource["identity"]["type"] = restObject["identity"].Value<string>("type");
            }
            resource["tags"] = restObject["tags"];
            resource["scale"] = null;
            resource["properties"] = new JObject();
            resource["properties"]["publisherEmail"] = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_publisherEmail", "string", restObject["properties"].Value<string>("publisherEmail")));
            resource["properties"]["publisherName"] = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_publisherName", "string", restObject["properties"].Value<string>("publisherName")));
            resource["properties"]["notificationSenderEmail"] = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_notificationSenderEmail", "string", restObject["properties"].Value<string>("notificationSenderEmail")));
            resource["properties"]["hostnameConfigurations"] = restObject["properties"]["hostnameConfigurations"];

            for (int i = 0; i < restObject["properties"]["hostnameConfigurations"].Value<JArray>().Count; i++)
            {
                var hostType = restObject["properties"]["hostnameConfigurations"][i].Value<string>("type");
                //check for custom hostname
                if (!string.IsNullOrEmpty(restObject["properties"]["hostnameConfigurations"][i].Value<string>("hostName")))
                {
                    resource["properties"]["hostnameConfigurations"][i]["hostName"] = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_{hostType}_hostName", "string", restObject["properties"]["hostnameConfigurations"][i].Value<string>("hostName")));
                }
                //check for keyVaultId, then parameterize and remove cert properties.
                if (!string.IsNullOrEmpty(restObject["properties"]["hostnameConfigurations"][i].Value<string>("keyVaultId")))
                {
                    resource["properties"]["hostnameConfigurations"][i]["keyVaultId"] = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_{hostType}_keyVaultId", "string", restObject["properties"]["hostnameConfigurations"][i].Value<string>("keyVaultId")));
                    resource["properties"]["hostnameConfigurations"][i]["certificate"]?.Parent.Remove();
                    resource["properties"]["hostnameConfigurations"][i]["encodedCertificate"]?.Parent.Remove();
                    resource["properties"]["hostnameConfigurations"][i]["certificatePassword"]?.Parent.Remove();
                }
            }
            resource["properties"]["additionalLocations"] = restObject["properties"]["additionalLocations"];
            resource["properties"]["customProperties"] = restObject["properties"]["customProperties"];
            var virtualNetworkTypeParameter = AddParameter($"{GetServiceName(servicename, false)}_virtualNetworkType", "string", restObject["properties"].Value<string>("virtualNetworkType"));
            resource["properties"]["virtualNetworkType"] = WrapParameterName(virtualNetworkTypeParameter);
            resource["properties"]["virtualNetworkConfiguration"] = $"[if(not(equals(parameters('{virtualNetworkTypeParameter}'), 'None')), variables('virtualNetworkConfiguration'), json('null'))]";
            this.resources.Add(resource);
            var vnc = GetVirtualNetworkConfigurationVariable(servicename, restObject["properties"]["virtualNetworkConfiguration"]);
            this.variables.Add("virtualNetworkConfiguration", vnc);
            APIMInstanceAdded = true;
            return resource;
        }

        private string GetVirtualnetworkParameter(string servicename, JToken virtualNetworkConfiguration, string propertyName)
        {
            return WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_virtualNetwork_{propertyName}",
                "string", virtualNetworkConfiguration.Type == JTokenType.Null
                    ? string.Empty
                    : virtualNetworkConfiguration.Value<string>(propertyName)), true);
        }

        public string GetServiceName(string servicename, bool addName = true)
        {
            if (fixedServiceNameParameter)
                return "apimServiceName";
            return addName ? $"service_{servicename}_name" : $"service_{servicename}";
        }

        public JObject AddApi(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");
            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/{name}')]" : $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/' ,parameters('{AddParameter($"api_{name}_name", "string", name)}'))]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            AddParameterFromObject((JObject)resource["properties"], "apiRevision", "string", name);
            AddParameterFromObject((JObject)resource["properties"], "serviceUrl", "string", name);
            AddParameterFromObject((JObject)resource["properties"], "apiVersion", "string", name);
            AddParameterFromObject((JObject)resource["properties"], "isCurrent", "bool", name);
            /*       Migrated to new version
             *       if (resource["properties"]?["subscriptionRequired"] != null)
                   {
                       resource["apiVersion"] = "2019-01-01";
                   }*/

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");
            }

            if (chainDependencies && lastApi != null)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('{GetServiceName(servicename)}'), '{lastApi}')]");
            }

            lastApi = name;
            resource["dependsOn"] = dependsOn;
            this.resources.Add(resource);
            return resource;
        }

        private JObject GetVirtualNetworkConfigurationVariable(string servicename, JToken virtualNetworkConfiguration)
        {
            var subnetnameParameter = AddParameter($"{GetServiceName(servicename, false)}_virtualNetwork_subnetname",
                "string", virtualNetworkConfiguration.Type == JTokenType.Null ? String.Empty : virtualNetworkConfiguration.Value<string>("subnetname") ?? String.Empty);
            return new JObject
            {
                ["subnetResourceId"] = GetVirtualnetworkParameter(servicename, virtualNetworkConfiguration, "subnetResourceId"),
                ["vnetid"] = GetVirtualnetworkParameter(servicename, virtualNetworkConfiguration, "vnetid"),
                ["subnetname"] = $"[if(equals(parameters('{subnetnameParameter}'), ''), json('null'), parameters('{subnetnameParameter}'))]"
            };
        }

        public ResourceTemplate CreateAPITag(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");
            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");
            string apiname = apiid.ValueAfter("apis");
            apiname = parametrizePropertiesOnly ? $"'{apiname}'" : $"parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}')";

            var obj = new ResourceTemplate();
            obj.AddName($"parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}')");
            obj.AddName(apiname);
            obj.AddName($"'{name}'");

            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.type = type;
            obj.properties = restObject.Value<JObject>("properties");

            if (APIMInstanceAdded)
            {
                obj.dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");
                //resource["dependsOn"] = new JArray(new string[] { $"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]" });
            }
            obj.dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('{GetServiceName(servicename)}'),{apiname})]");

            return obj;
        }

        public ResourceTemplate CreateAPISchema(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");
            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");
            string apiname = apiid.ValueAfter("apis");
            apiname = parametrizePropertiesOnly ? $"'{apiname}'" : $"parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}')";

            var obj = new ResourceTemplate();
            obj.AddName($"parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}')");
            obj.AddName(apiname);
            obj.AddName($"'{name}'");

            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.type = type;
            obj.properties = restObject.Value<JObject>("properties");

            if (APIMInstanceAdded)
            {
                obj.dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");
                //resource["dependsOn"] = new JArray(new string[] { $"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]" });
            }
            obj.dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('{GetServiceName(servicename)}'),{apiname})]");

            return obj;
        }

        public JObject CreateOperation(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");

            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");
            string apiname = apiid.ValueAfter("apis");

            name = $"'{name}'";
            apiname = parametrizePropertiesOnly ? $"'{apiname}'" : $"parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}')";

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/', {apiname}, '/', {name})]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            //Schema list
            var schemalist = new List<string>();

            var request = resource["properties"].Value<JObject>("request");
            if (request != null)
            {
                schemalist = schemalist.Union(FixRepresentations(request.Value<JArray>("representations"))).ToList();
            }

            var responses = resource["properties"].Value<JArray>("responses");
            if (responses != null)
            {
                foreach (var resp in responses)
                {
                    schemalist = schemalist.Union(FixRepresentations(resp.Value<JArray>("representations"))).ToList();
                }
            }


            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");
            }
            foreach (var schema in schemalist)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis/schemas', parameters('{GetServiceName(servicename)}'),{apiname},'{schema}')]");
            }

            dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('{GetServiceName(servicename)}'),{apiname})]");

            resource["dependsOn"] = dependsOn;

            return resource;
            //this.resources.Add(resource);
        }

        private List<string> FixRepresentations(JArray reps)
        {
            var ll = new List<string>();
            if (reps == null)
                return ll;
            foreach (JObject rep in reps)
            {
                string sample = rep.Value<string>("sample") ?? "";
                //if sample is an arrau and start with [ it need's to be escaped
                if (sample.StartsWith("["))
                    rep["sample"] = "[" + sample;

                string generatedSample = rep.Value<string>("generatedSample") ?? "";
                if (generatedSample.StartsWith("["))
                    rep["generatedSample"] = "[" + generatedSample;


                var schema = rep.Value<string>("schemaId");
                if (!string.IsNullOrEmpty(schema))
                    ll.Add(schema);
            }
            return ll;
        }

        public Property AddBackend(JObject restObject, JObject azureResource)
        {
            Property retval = null;
            if (restObject == null)
                return retval;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");

            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/' ,'{name}')]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            var dependsOn = new JArray();
            if (restObject["properties"]["resourceId"] != null)
            {
                string resourceid = restObject["properties"].Value<string>("resourceId");
                var aid = new AzureResourceId(resourceid.Replace("https://management.azure.com/", ""));
                aid.SubscriptionId = "',subscription().subscriptionId,'";
                var rgparamname = AddParameter(name + "_resourceGroup", "string", aid.ResourceGroupName);
                aid.ResourceGroupName = "',parameters('" + rgparamname + "'),'";
                if (resourceid.Contains("providers/Microsoft.Logic/workflows")) //Logic App
                {
                    var laname = aid.ValueAfter("workflows");
                    var logicappname = AddParameter(name + "_logicAppName", "string", laname);
                    aid.ReplaceValueAfter("workflows", "',parameters('" + logicappname + "')");


                    var triggerObject = azureResource["properties"]["definition"].Value<JObject>("triggers");
                    string triggername = "manual";
                    foreach (var trigger in triggerObject)
                    {
                        if (trigger.Value.Value<string>("type") == "Request" && trigger.Value.Value<string>("kind") == "Http")
                        {
                            triggername = trigger.Key;
                        }
                    }
                    //need to get the Logic App triggers and find the HTTP one....

                    string listcallbackref = $"listCallbackUrl(resourceId(parameters('{rgparamname}'), 'Microsoft.Logic/workflows/triggers', parameters('{logicappname}'), '{triggername}'), '2017-07-01')";

                    resource["properties"]["url"] = $"[substring({listcallbackref}.basePath,0,add(10,indexOf({listcallbackref}.basePath,'/triggers/')))]";
                    retval = new Property()
                    {
                        type = Property.PropertyType.LogicApp,
                        name = laname.ToLower(),
                        extraInfo = listcallbackref
                    };
                }
                else if (resourceid.Contains("providers/Microsoft.Web/sites")) //Web App/Function
                {
                    var sitename = aid.ValueAfter("sites");
                    var paramsitename = AddParameter(name + "_siteName", "string", sitename);
                    aid.ReplaceValueAfter("sites", "',parameters('" + paramsitename + "')");
                    resource["properties"]["description"] = $"[parameters('{paramsitename}')]";
                    string path = GetPathFromUrl(resource["properties"]?.Value<string>("url"));
                    resource["properties"]["url"] = $"[concat('https://',toLower(parameters('{paramsitename}')),'.azurewebsites.net/{path}')]";

                    //Determine the extrainfo based on the parameterizeBackendFunctionKey. When the backend should be parameterized use the name of the property
                    //in the x-functions-key header
                    //var extraInfo = $"listsecrets(resourceId(parameters('{rgparamname}'),'Microsoft.Web/sites/functions', parameters('{paramsitename}'), 'replacewithfunctionoperationname'),'2015-08-01').key";
                    var extraInfo = $"listKeys(resourceId(parameters('{rgparamname}'),concat('Microsoft.Web/sites/host'),parameters('{paramsitename}'),'default'),'2018-02-01').functionKeys.default";
                    var functionAppPropertyName = sitename;
                    if (parameterizeBackendFunctionKey)
                    {
                        var custom = false;

                        var xFunctionKey = (resource["properties"]?["credentials"]?["header"]?["x-functions-key"] ?? new JArray()).FirstOrDefault(); ;
                        if (xFunctionKey != null)
                        {
                            var value = xFunctionKey.Value<string>();
                            if (value.StartsWith("{{") && value.EndsWith("}}"))
                            {
                                var parsed = value.Substring(2, value.Length - 4);
                                functionAppPropertyName = parsed;
                                extraInfo = $"parameters('{AddParameter($"{parsed}", "string", "")}')";
                                custom = true;
                            }
                        }

                        if (!custom)
                        {
                            functionAppPropertyName = $"{sitename}-key";
                            extraInfo = $"parameters('{AddParameter($"{sitename}-key", "string", "")}')";
                        }
                    }

                    retval = new Property()
                    {
                        type = Property.PropertyType.Function,
                        name = functionAppPropertyName.ToLower(),

                        extraInfo = extraInfo
                    };

                    var code = (resource["properties"]?["credentials"]?["query"]?.Value<JArray>("code") ?? new JArray()).FirstOrDefault();
                    if (code == null)
                    {
                        //Fall back to the x functions key
                        code = (resource["properties"]?["credentials"]?["header"]?["x-functions-key"] ?? new JArray()).FirstOrDefault(); ;
                    }

                    if (code != null)
                    {
                        var value = code.Value<string>();
                        if (value.StartsWith("{{") && value.EndsWith("}}") && parameterizeBackendFunctionKey)
                        {
                            var parsed = value.Substring(2, value.Length - 4);
                            dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/namedValues', parameters('{GetServiceName(servicename)}'),'{parsed}')]");
                        }
                    }
                }
                resource["properties"]["resourceId"] = "[concat('https://management.azure.com/','" + aid.ToString().Substring(1) + ")]";
            }
            else
            {
                AddParameterFromObject((JObject)resource["properties"], "url", "string", name);

                //todo: tried to add namedvalues used in credetials to template. Doesn't work yet.
                //var queries = resource["properties"]?["credentials"]?.Value<JObject>("query");
                //if (queries != null)
                //{
                    
                //    //HandleProperties(logger.Value<string>("name"), "Logger", logger["properties"].ToString());


                //    foreach (var query in queries.Properties())
                //    {
                //        foreach (string value in query.Value)
                //        {
                //            if (!value.StartsWith("{{") || !value.EndsWith("}}"))
                //                continue;

                //            var parsed = value.Substring(2, value.Length - 4);
                //            dependsOn.Add(
                //                $"[resourceId('Microsoft.ApiManagement/service/namedValues', parameters('{GetServiceName(servicename)}'),'{parsed}')]");
                //        }
                //    }
                //}

                AddParameterFromObject((JObject)resource["properties"], "credentials", "secureobject", name);
            }

            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");
            }

            if (dependsOn.Count > 0)
            {
                resource["dependsOn"] = dependsOn;
            }

            if (this.resources.Where(rr => rr.Value<string>("name") == obj.name).Count() == 0)
                this.resources.Add(resource);

            return retval;
        }

        private string GetPathFromUrl(string url)
        {
            if (String.IsNullOrWhiteSpace(url))
                return String.Empty;
            var uri = new Uri(url);
            return uri.PathAndQuery.Substring(1);
        }

        public ResourceTemplate AddVersionSet(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");


            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.AddName($"parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}')");
            obj.AddName($"'{name}'");

            obj.type = type;
            obj.properties = restObject.Value<JObject>("properties");
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");

            }
            resource["dependsOn"] = dependsOn;
            this.resources.Add(resource);
            return obj;
        }

        public void AddGroup(JObject restObject)
        {
            if (restObject == null)
                return;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");


            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/{name}')]" : $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/' ,parameters('{AddParameter($"group_{name}_name", "string", name)}'))]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");

            }
            resource["dependsOn"] = dependsOn;

            // Avoid duplicates.
            if (this.resources.Count(rr => rr.Value<string>("name") == obj.name && rr.Value<string>("type") == obj.type) == 0)
            {
                this.resources.Add(resource);
            }
        }

        public JObject AddProduct(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");

            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/{name}')]" : $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/' ,parameters('{AddParameter($"product_{name}_name", "string", name)}'))]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");

            }
            resource["dependsOn"] = dependsOn;

            this.resources.Add(resource);
            return resource;
        }

        public JObject AddProductSubObject(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");

            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");
            string productname = apiid.ValueAfter("products");

            productname = parametrizePropertiesOnly ? $"'{productname}'" : $"parameters('{AddParameter($"product_{productname}_name", "string", productname)}')";
            var objectname = "";
            if (parametrizePropertiesOnly)
            {
                objectname = $"'{name}'";
            }
            else
            {
                switch (type)
                {
                    case "Microsoft.ApiManagement/service/products/apis":
                        {
                            objectname = $"parameters('{AddParameter($"api_{name}_name", "string", name)}')";
                            break;
                        }
                    case "Microsoft.ApiManagement/service/products/groups":
                        {
                            objectname = $"parameters('{AddParameter($"group_{name}_name", "string", name)}')";
                            break;
                        }
                    default:
                        {
                            objectname = $"'{name}'";
                            break;
                        }
                }
            }

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/', {productname}, '/', {objectname})]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            var dependsOn = new JArray();
            dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/products', parameters('{GetServiceName(servicename)}'), {productname})]");

            if (type == "Microsoft.ApiManagement/service/products/apis")
            {
                // products/apis have a dependency on the product and the api.
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('{GetServiceName(servicename)}'), '{name}')]");

                if (chainDependencies && lastProductApi != null)
                {
                    dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/products/apis', parameters('{GetServiceName(servicename)}'), {productname}, {lastProductApi})]");
                }

                lastProductApi = objectname;
            }

            resource["dependsOn"] = dependsOn;

            return resource;
        }

        public ResourceTemplate AddNamedValues(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");
            bool secret = restObject["properties"].Value<bool>("secret");

            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.AddName($"parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}')");
            obj.AddName($"'{name}'");
            obj.apiVersion = "2020-06-01-preview";

            obj.type = type;
            obj.properties = restObject.Value<JObject>("properties");
            var resource = JObject.FromObject(obj);


            //is key vault? 
            var KeyVaultObj = resource["properties"].Value<JObject>("keyVault");
            if (KeyVaultObj != null)
            {
                /*
                 {
                  "secretIdentifier": "https://kv-re-dev-api-euw.vault.azure.net/secrets/centiro-username",
                  "identityClientId": null,
                  "lastStatus": {
                    "code": "Success",
                    "timeStampUtc": "2021-03-23T11:39:33.4731812Z"
                  }
                 */
                var kvIdentifier = KeyVaultObj.Value<string>("secretIdentifier");
                var match = Regex.Match(kvIdentifier, "https://(?<keyvaultname>.*).vault.azure.net/secrets/(?<secretname>[^*#&+:<>?/]+)(/(?<secretversion>.*))?");
                if (match.Success)
                {
                    var keyvaultName = match.Groups["keyvaultname"].Value;
                    var secretname = match.Groups["secretname"].Value;
                    var secretversion = match.Groups["secretversion"];
                    resource["properties"]["keyVault"] = new JObject();
                    var parameterKeyVaultName = (fixedKeyVaultNameParameter) ? "keyVaultName" : restObject["properties"].Value<string>("displayName") + "_" + "keyVaultName";
                    resource["properties"]["keyVault"]["secretIdentifier"] = $"[concat('https://'," +
                        $"{WrapParameterName(this.AddParameter(parameterKeyVaultName, "string", keyvaultName), brackets: false)}, '.vault.azure.net/secrets/'," +
                        $"{WrapParameterName(this.AddParameter(restObject["properties"].Value<string>("displayName") + "_" + "secretName", "string", secretname), brackets: false)}" +
                        $"{(secretversion.Success ? (",'/'," + WrapParameterName(this.AddParameter(restObject["properties"].Value<string>("displayName") + "_" + "secretVersion", "string", secretversion.Value), brackets: false)) : String.Empty)})]";
                }
            }
            else
            {
                var propValue = resource["properties"].Value<string>("value") ?? "";

                if (!((propValue.StartsWith("[") && propValue.EndsWith("]"))))
                {
                    resource["properties"]["value"] = WrapParameterName(this.AddParameter(restObject["properties"].Value<string>("displayName") + "_" + "value", secret ? "securestring" : "string", secret ? "secretvalue" : propValue));
                }
            }

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");

            }
            resource["dependsOn"] = dependsOn;

            this.resources.Add(resource);
            return obj;
        }

        //need to return an object with property list and so on
        public JObject CreatePolicy(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");
            string servicename = "";
            string apiname = "";
            string operationname = "";
            bool servicePolicy = false;

            name = $"'{name}'";


            var rid = new AzureResourceId(restObject.Value<string>("id"));
            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            if (type == "Microsoft.ApiManagement/service/apis/policies")
            {
                servicename = rid.ValueAfter("service");
                apiname = rid.ValueAfter("apis");

                apiname = parametrizePropertiesOnly ? $"'{apiname}'" : $"parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}')";
                obj.name = $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/', {apiname}, '/', {name})]";
            }
            else if (type == "Microsoft.ApiManagement/service/apis/operations/policies")
            {
                servicename = rid.ValueAfter("service");
                apiname = rid.ValueAfter("apis");
                operationname = rid.ValueAfter("operations");
                apiname = parametrizePropertiesOnly ? $"'{apiname}'" : $"parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}')";
                operationname = $"'{operationname}'";
                obj.name = $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/', {apiname}, '/', {operationname}, '/', {name})]";
            }
            else if (type == "Microsoft.ApiManagement/service/policies")
            {
                servicename = rid.ValueAfter("service");
                obj.name = $"[concat(parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), '/', 'policy')]";
                servicePolicy = true;
            }

            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");
            }

            if (!servicePolicy)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('{GetServiceName(servicename)}') , {apiname})]");
            }

            if (type == "Microsoft.ApiManagement/service/apis/operations/policies")
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis/operations', parameters('{GetServiceName(servicename)}'), {apiname}, {operationname})]");
            }

            resource["dependsOn"] = dependsOn;
            return resource;
            //this.resources.Add(resource);
        }

        public JObject AddApplicationInsightsInstance(JObject restObject)
        {
            var obj = new ResourceTemplate
            {
                comments = "Generated for resource " + restObject.Value<string>("id"),
                type = "Microsoft.Insights/components",
            };
            var rid = new AzureResourceId(restObject.Value<string>("id"));
            var servicename = rid.ValueAfter("service");
            string name = restObject.Value<string>("name");
            obj.name = WrapParameterName(AddParameter($"{GetServiceName(servicename, false)}_applicationInsights", "string", name));
            var resource = JObject.FromObject(obj);
            resource["location"] = WrapParameterName($"{GetServiceName(servicename, false)}_location");
            resource["apiVersion"] = "2015-05-01";
            resource["kind"] = "other";
            resource["properties"] = JObject.FromObject(new { Application_Type = "other" });
            if (APIMInstanceAdded)
            {
                resource["dependsOn"] = new JArray
                {
                    $"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]"
                };
            }
            return resource;
        }

        public JObject CreateServiceResource(JObject restObject, string resourceType, bool addResource)
        {
            var obj = new ResourceTemplate
            {
                comments = "Generated for resource " + restObject.Value<string>("id"),
                type = resourceType
            };
            var rid = new AzureResourceId(restObject.Value<string>("id"));
            var servicename = rid.ValueAfter("service");
            obj.AddName($"parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}')");
            string name = GetServiceResourceName(restObject, resourceType);
            obj.AddName(name);
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];
            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('{GetServiceName(servicename)}'))]");
            }
            resource["dependsOn"] = dependsOn;
            if (addResource)
            {
                if (resources.All(x => x.Value<string>("name") != resource.Value<string>("name")))
                    resources.Add(resource);
            }

            return resource;
        }

        private string GetServiceResourceName(JObject restObject, string resourceType)
        {
            var rid = new AzureResourceId(restObject.Value<string>("id"));
            var servicename = rid.ValueAfter("service");
            var name = restObject.Value<string>("name");
            var resourceTypeShort = GetResourceTypeShort(resourceType);
            bool applicationInsightsLogger = IsApplicationInsightsLogger(restObject);
            if (applicationInsightsLogger)
                name = $"parameters('{AddParameter($"{GetServiceName(servicename, false)}_applicationInsights", "string", name)}')";
            else
                name = parametrizePropertiesOnly ? $"'{name}'" : $"parameters('{AddParameter($"{resourceTypeShort}_{name}_name", "string", name)}')";
            return name;
        }

        private bool IsApplicationInsightsLogger(JObject restObject)
        {
            if (restObject.Value<string>("type") != "Microsoft.ApiManagement/service/loggers")
                return false;
            if (restObject["properties"]?["loggerType"]?.Value<string>() != "applicationInsights")
                return false;
            return true;
        }

        private string GetResourceTypeShort(string resourceType)
        {
            var split = resourceType.Split('/');
            var type = split[split.Length - 1];
            if (type.EndsWith("s"))
                return type.Substring(0, type.Length - 1);
            return type;
        }

        public JObject CreateCertificate(JObject restObject, bool addResource)
        {
            var resource = CreateServiceResource(restObject, "Microsoft.ApiManagement/service/certificates", addResource);
            var certificatename = new AzureResourceId(restObject.Value<string>("id")).ValueAfter("certificates");
            var properties = new
            {
                data = WrapParameterName(AddParameter($"certificate_{certificatename}_data", "securestring", String.Empty)),
                password = WrapParameterName(AddParameter($"certificate_{certificatename}_password", "securestring", String.Empty))
            };
            resource["properties"] = JObject.FromObject(properties);
            return resource;
        }

        public JObject CreateLogger(JObject restObject, bool addResource)
        {
            var resource = CreateServiceResource(restObject, "Microsoft.ApiManagement/service/loggers", addResource);
            var azureResourceId = new AzureResourceId(restObject.Value<string>("id"));
            var loggerName = azureResourceId.ValueAfter("loggers");
            var serviceName = azureResourceId.ValueAfter("service");
            var credentials = resource["properties"]?["credentials"];
            if (credentials != null)
            {
                if (credentials.Value<string>("connectionString") != null)
                {
                    credentials["connectionString"] = WrapParameterName(AddParameter($"logger_{loggerName}_connectionString", "securestring", String.Empty));
                }
                var loggerType = resource["properties"]?["loggerType"]?.Value<string>() ?? string.Empty;
                if (referenceApplicationInsightsInstrumentationKey && loggerType == "applicationInsights" && credentials.Value<string>("instrumentationKey") != null)
                {
                    string parameter = AddParameter($"{GetServiceName(serviceName, false)}_applicationInsights", "string", loggerName);
                    credentials["instrumentationKey"] = $"[reference(resourceId('Microsoft.Insights/components', parameters('{parameter}')), '2014-04-01').InstrumentationKey]";
                    var dependsOn = resource.Value<JArray>("dependsOn") ?? new JArray();
                    dependsOn.Add($"[resourceId('Microsoft.Insights/components',parameters('{GetServiceName(serviceName, false)}_applicationInsights'))]");
                    resource["dependsOn"] = dependsOn;

                }
                if (credentials.Value<string>("name") != null)
                {
                    credentials["name"] = WrapParameterName(AddParameter($"logger_{loggerName}_credentialName", "string", GetDefaultValue(resource, "credentials", "name")));
                }
            }

            if (resource["properties"] is JObject properties)
            {
                //remove resourceId, because this is not used.
                properties.Remove("resourceId");
            }

            return resource;
        }
        public JObject CreateIdentityProvider(JObject restObject, bool addResource)
        {
            var resource = CreateServiceResource(restObject, "Microsoft.ApiManagement/service/identityProviders", addResource);
            var properties = resource["properties"];
            var name = restObject.Value<string>("name");
            if (properties?.Value<string>("clientId") != null)
            {
                properties["clientId"] = WrapParameterName(AddParameter($"identityProvider_{name}_clientId", "string", properties.Value<string>("clientId")));
            }
            if (properties?.Value<string>("clientSecret") != null)
            {
                properties["clientSecret"] = WrapParameterName(AddParameter($"identityProvider_{name}_clientSecret", "securestring", String.Empty));
            }
            return resource;
        }
        public JObject CreateTags(JObject restObject, bool addResource)
        {
            var resource = CreateServiceResource(restObject, "Microsoft.ApiManagement/service/tags", addResource);
            var properties = resource["properties"];

            return resource;
        }

        public JObject CreateApiDiagnostic(JObject restObject, JArray loggers, bool addResource)
        {
            var resource = CreateServiceResource(restObject, "Microsoft.ApiManagement/service/apis/diagnostics", addResource);
            ResourceTemplate obj = restObject.ToObject<ResourceTemplate>();
            obj.comments = resource.Value<string>("comments");
            AzureResourceId apiid = new AzureResourceId(restObject.Value<string>("id"));
            string servicename = apiid.ValueAfter("service");
            string apiname = apiid.ValueAfter("apis");
            apiname = parametrizePropertiesOnly ? $"'{apiname}'" : $"parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}')";

            var loggerId = restObject["properties"]?.Value<string>("loggerId") ?? String.Empty;
            var logger = loggers.FirstOrDefault(x => x.Value<string>("id") == loggerId);



            //redefine name
            string name = obj.name;
            obj.name = null;
            obj.AddName($"parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}')");
            obj.AddName(apiname);
            obj.AddName($"parameters('{AddParameter($"diagnostic_{name}_name", "string", name)}')");

            if (logger != null)
            {
                var rid = new AzureResourceId(restObject.Value<string>("id"));
                JObject loggerObject = JObject.FromObject(logger);
                var loggerName = GetServiceResourceName(loggerObject, "Microsoft.ApiManagement/service/loggers");
                string loggerResource = $"[resourceId('Microsoft.ApiManagement/service/loggers', parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), {loggerName})]";

                //set apiVersion to 2019-01-01
                obj.apiVersion = "2019-01-01";

                obj.properties["loggerId"] = loggerResource;
                obj.properties["alwaysLog"] = WrapParameterName(AddParameter($"diagnostic_{name}_alwaysLog", "string", GetDefaultValue(restObject, "alwaysLog")), true);

                obj.properties["sampling"]["percentage"] = WrapParameterName(AddParameter($"diagnostic_{name}_samplingPercentage", "string", GetDefaultValue(restObject, "sampling", "percentage")));

                //add when logger object is added
                //obj.dependsOn.Add(loggerResource);
                if (IsApplicationInsightsLogger(loggerObject))
                {
                    obj.properties["enableHttpCorrelationHeaders"] = WrapParameterName(AddParameter($"diagnostic_{name}_enableHttpCorrelationHeaders", "bool", GetDefaultValue(resource, true, "enableHttpCorrelationHeaders")));
                }
            }

            obj.dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('{GetServiceName(servicename)}'), {apiname})]");
            return JObject.FromObject(obj);
        }

        public JObject CreateDiagnostic(JObject restObject, JArray loggers, bool addResource)
        {
            var resource = CreateServiceResource(restObject, "Microsoft.ApiManagement/service/diagnostics", addResource);
            var properties = resource["properties"];
            var name = restObject.Value<string>("name");
            var loggerId = restObject["properties"]?.Value<string>("loggerId") ?? String.Empty;
            var logger = loggers.FirstOrDefault(x => x.Value<string>("id") == loggerId);
            resource["apiVersion"] = "2019-01-01";
            if (logger != null)
            {
                var rid = new AzureResourceId(restObject.Value<string>("id"));
                var servicename = rid.ValueAfter("service");
                JObject loggerObject = JObject.FromObject(logger);
                var loggerName = GetServiceResourceName(loggerObject, "Microsoft.ApiManagement/service/loggers");
                string loggerResource = $"[resourceId('Microsoft.ApiManagement/service/loggers', parameters('{AddParameter($"{GetServiceName(servicename)}", "string", servicename)}'), {loggerName})]";
                properties["loggerId"] = loggerResource;
                resource.Value<JArray>("dependsOn").Add(loggerResource);
                resource["properties"]["alwaysLog"] = WrapParameterName(AddParameter($"diagnostic_{name}_alwaysLog", "string", GetDefaultValue(resource, "alwaysLog")));
                resource["properties"]["sampling"]["percentage"] = WrapParameterName(AddParameter($"diagnostic_{name}_samplingPercentage", "string", GetDefaultValue(resource, "sampling", "percentage")));
                if (IsApplicationInsightsLogger(loggerObject))
                {
                    var value =
                    resource["properties"]["enableHttpCorrelationHeaders"] = WrapParameterName(AddParameter($"diagnostic_{name}_enableHttpCorrelationHeaders", "bool", GetDefaultValue(resource, true, "enableHttpCorrelationHeaders")));
                }
            }
            return resource;
        }

        public JObject CreateOpenIDConnectProvider(JObject restObject, bool addResource)
        {
            var resource = CreateServiceResource(restObject, "Microsoft.ApiManagement/service/openidConnectProviders", addResource);
            var providerName = new AzureResourceId(restObject.Value<string>("id")).ValueAfter("openidConnectProviders");
            resource["properties"]["displayName"] = WrapParameterName(AddParameter($"openidConnectProvider_{providerName}_displayname", "string", GetDefaultValue(resource, "displayName")));
            resource["properties"]["metadataEndpoint"] = WrapParameterName(AddParameter($"openidConnectProvider_{providerName}_metadataEndpoint", "string", GetDefaultValue(resource, "metadataEndpoint")));
            resource["properties"]["clientId"] = WrapParameterName(AddParameter($"openidConnectProvider_{providerName}_clientId", "string", GetDefaultValue(resource, "clientId")));
            resource["properties"]["clientSecret"] = WrapParameterName(AddParameter($"openidConnectProvider_{providerName}_clientSecret", "securestring", String.Empty));
            return resource;
        }

        private static string GetDefaultValue(JObject resource, params string[] names)
        {
            var prop = resource["properties"];
            foreach (var name in names)
            {
                prop = prop[name];
                if (prop == null)
                    return string.Empty;
            }
            return prop.Value<string>() ?? String.Empty;
        }

        private static T GetDefaultValue<T>(JObject resource, T defaultValue, params string[] names)
        {
            var prop = resource["properties"];
            foreach (var name in names)
            {
                prop = prop[name];
                if (prop == null)
                    return defaultValue;
            }

            var retVal = prop.Value<T>();

            if (retVal == null)
            {
                retVal = defaultValue;
            }

            return retVal;
        }

        public JObject CreateBackend(JObject restObject)
        {
            var resource = CreateServiceResource(restObject, "Microsoft.ApiManagement/service/backends", false);
            return resource;
        }

        public void RemoveResources_BuiltInGroups()
        {
            var groups = this.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/groups" && rr["properties"].Value<string>("type") == "system");
            int count = groups.Count();
            for (int i = 0; i < count; i++)
            {
                RemoveResource(groups.First());

            }
        }

        public void ParameterizeAPIs()
        {
            var apis = this.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis");

            foreach (var api in apis)
            {

                var name = GetParameterName(((string)api["name"]), "_name");
                AddParameterFromObject((JObject)api["properties"], "serviceUrl", "string", name);
                AddParameterFromObject((JObject)api["properties"], "apiRevision", "string", name);
            }
        }

        public void ParameterizeBackends()
        {
            var backends = this.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/backends");

            foreach (var backend in backends)
            {
                var name = GetParameterName(((string)backend["name"]), "_name");
                AddParameterFromObject((JObject)backend["properties"], "url", "string", name);

            }
        }

        public void ParameterizeAuthorizationServers()
        {
            var resources = this.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/authorizationServers");

            foreach (var resource in resources)
            {
                var name = GetParameterName(((string)resource["name"]), "_name");
                AddParameterFromObject((JObject)resource["properties"], "clientRegistrationEndpoint", "string", name);
                AddParameterFromObject((JObject)resource["properties"], "authorizationEndpoint", "string", name);
                AddParameterFromObject((JObject)resource["properties"], "tokenEndpoint", "string", name);
                AddParameterFromObject((JObject)resource["properties"], "clientSecret", "securestring", name);
                AddParameterFromObject((JObject)resource["properties"], "resourceOwnerUsername", "string", name);
                AddParameterFromObject((JObject)resource["properties"], "resourceOwnerPassword", "string", name);

                //handle clientid                
                var orgclientidvalue = resource["properties"].Value<string>("clientId");
                var matches = System.Text.RegularExpressions.Regex.Match(orgclientidvalue, "'(?<val>[a-zA-Z0-9_-]*)'\\)\\]");
                if (matches.Groups.Count > 0)
                {
                    var orgvalue = matches.Groups["val"].Value;
                    var paramname = this.AddParameter(name + "_clientId", "securestring", orgvalue);

                    resource["properties"]["clientId"] = orgclientidvalue.Replace($"'{orgvalue}'", WrapParameterName(paramname));
                }

            }
        }


        /** PREVIEW FIXES **/
        public void FixOperationsMissingUrlTemplateParameters()
        {

            var resources = this.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis/operations");

            foreach (var resource in resources)
            {
                string urlTemplate = resource["properties"].Value<string>("urlTemplate");
                JArray templateParameters = (JArray)resource["properties"]["templateParameters"];
                if (!string.IsNullOrEmpty(urlTemplate))
                {
                    var match = Regex.Match(urlTemplate, "{[a-zA-Z0-9_-]*}");
                    while (match.Success)
                    {
                        if (templateParameters == null)
                        {
                            templateParameters = new JArray();
                            resource["properties"]["templateParameters"] = templateParameters;
                        }
                        var name = match.Value.Replace("{", "").Replace("}", "");
                        var obj = new JObject();
                        obj.Add("name", name);
                        obj.Add("type", "string");
                        obj.Add("required", true);
                        templateParameters.Add(obj);
                        match = match.NextMatch();
                    }
                }
            }
        }
    }
}
