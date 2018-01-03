using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

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

        public DeploymentTemplate(bool parametrizePropertiesOnly = false)
        {
            parameters = new JObject();
            variables = new JObject();
            resources = new List<JObject>();
            outputs = new JObject();

            this.parametrizePropertiesOnly = parametrizePropertiesOnly;
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

        public string AddParameter(string paramname, string type, string defaultvalue)
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


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string WrapParameterName(string paramname)
        {
            return "[parameters('" + paramname + "')]";
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
            var propValue = (string)obj[propertyName];
            if (propValue.StartsWith("[") && propValue.EndsWith("]"))
                return;
            obj[propertyName] = WrapParameterName(this.AddParameter(paramNamePrefix + "_" + propertyName, propertyType, propValue));
        }

        /**
         * 
         *  API Management pecifics
         * 
         */

        private bool APIMInstanceAdded = false;
        private string apimservicename;

        public void AddAPIManagementInstance(JObject restObject)
        {
            if (restObject == null)
                return;

            string servicename = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");
            apimservicename = servicename;
            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = WrapParameterName(AddParameter($"APIM_servicename", "string", servicename));
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["sku"] = restObject["sku"];
            resource["sku"]["name"] = WrapParameterName(AddParameter($"service_{servicename}_sku_name", "string", restObject["sku"].Value<string>("name")));
            resource["sku"]["capacity"] = WrapParameterName(AddParameter($"service_{servicename}_sku_capacity", "string", restObject["sku"].Value<string>("capacity")));
            resource["location"] = WrapParameterName(AddParameter($"service_{servicename}_location", "string", restObject.Value<string>("location")));
            resource["tags"] = restObject["tags"];
            resource["scale"] = null;
            resource["properties"] = new JObject();
            resource["properties"]["publisherEmail"] = WrapParameterName(AddParameter($"service_{servicename}_publisherEmail", "string", restObject["properties"].Value<string>("publisherEmail")));
            resource["properties"]["publisherName"] = WrapParameterName(AddParameter($"service_{servicename}_publisherName", "string", restObject["properties"].Value<string>("publisherName")));
            resource["properties"]["notificationSenderEmail"] = WrapParameterName(AddParameter($"service_{servicename}_notificationSenderEmail", "string", restObject["properties"].Value<string>("notificationSenderEmail")));
            resource["properties"]["hostnameConfigurations"] = restObject["properties"]["hostnameConfigurations"];
            resource["properties"]["additionalLocations"] = restObject["properties"]["additionalLocations"];
            resource["properties"]["virtualNetworkConfiguration"] = restObject["properties"]["virtualNetworkConfiguration"];
            resource["properties"]["customProperties"] = restObject["properties"]["customProperties"];
            resource["properties"]["virtualNetworkType"] = restObject["properties"]["virtualNetworkType"];
            this.resources.Add(resource);
            APIMInstanceAdded = true;
        }

        public JObject AddApi(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");

            var matches = Regex.Match(restObject.Value<string>("id"), "/service/(?<servicename>[a-zA-Z0-9_-]*)/");
            string servicename = matches.Groups["servicename"].Value;

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/{name}')]" : $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/' ,parameters('{AddParameter($"api_{name}_name", "string", name)}'))]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            if (!parametrizePropertiesOnly)
            {
                AddParameterFromObject((JObject)resource["properties"], "apiRevision", "string", name);
                AddParameterFromObject((JObject)resource["properties"], "serviceUrl", "string", name);
            }

            if (APIMInstanceAdded)
            {
                resource["dependsOn"] = new JArray(new string[] { $"[resourceId('Microsoft.ApiManagement/service', parameters('APIM_servicename'))]" });
            }else
            {
                resource["dependsOn"] = new JArray(); ;
            }

            this.resources.Add(resource);
            return resource;
        }

        public JObject CreateOperation(JObject restObject)
        {
            if (restObject == null)
                return null;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");


            var matches = Regex.Match(restObject.Value<string>("id"), "/service/(?<servicename>[a-zA-Z0-9_-]*)/apis/(?<apiname>[a-zA-Z0-9_-]*)");
            string servicename = matches.Groups["servicename"].Value;
            string apiname = matches.Groups["apiname"].Value;



            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/{apiname}/{name}')]" : $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/' ,parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}'), '/' ,parameters('{AddParameter($"operations_{name}_name", "string", name)}'))]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            //schemaId is not handled well yet.. so Reseting it for now

            var request = resource["properties"].Value<JObject>("request");
            if (request != null)
            {
                FixRepresentations(request.Value<JArray>("representations"));
            }

            var responses = resource["properties"].Value<JArray>("responses");
            if (responses != null)
            {
                foreach (var resp in responses)
                {
                    FixRepresentations(resp.Value<JArray>("representations"));
                }
            }


            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('APIM_servicename'))]");

            }

            if (parametrizePropertiesOnly)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('APIM_servicename'), '{apiname}')]");
            }
            else
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('APIM_servicename'), parameters('api_{apiname}_name'))]");
            }

            resource["dependsOn"] = dependsOn;

            return resource;
            //this.resources.Add(resource);
        }

        private void FixRepresentations(JArray reps)
        {
            if (reps == null)
                return;
            foreach (JObject rep in reps)
            {
                string sample = rep.Value<string>("sample") ?? "";
                //if sample is an arrau and start with [ it need's to be escaped
                if (sample.StartsWith("["))
                    rep["sample"] = "[" + sample;

                //temporary fix the schema until we have better solution TODO
                rep["schemaId"] = null;
            }
        }

        public void AddBackend(JObject restObject)
        {
            if (restObject == null)
                return;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");

            var matches = Regex.Match(restObject.Value<string>("id"), "/service/(?<servicename>[a-zA-Z0-9_-]*)/");
            string servicename = matches.Groups["servicename"].Value;

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/' ,parameters('{AddParameter($"backend_{name}_name", "string", name)}'))]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            AddParameterFromObject((JObject)resource["properties"], "url", "string", name);

            if (APIMInstanceAdded)
            {
                resource["dependsOn"] = new JArray(new string[] { $"[resourceId('Microsoft.ApiManagement/service', parameters('APIM_servicename'))]" });
            }

            this.resources.Add(resource);
        }
        public void AddGroup(JObject restObject)
        {
            if (restObject == null)
                return;

            string name = restObject.Value<string>("name");
            string type = restObject.Value<string>("type");


            var matches = Regex.Match(restObject.Value<string>("id"), "/service/(?<servicename>[a-zA-Z0-9_-]*)/");
            string servicename = matches.Groups["servicename"].Value;



            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/{name}')]" : $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/' ,parameters('{AddParameter($"group_{name}_name", "string", name)}'))]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('APIM_servicename'))]");

            }
            resource["dependsOn"] = dependsOn;

            this.resources.Add(resource);
        }

        public void AddProperty(JObject restObject)
        {
            if (restObject == null)
                return;

            string name = restObject["properties"].Value<string>("displayName");
            string type = restObject.Value<string>("type");
            bool secret = restObject["properties"].Value<bool>("secret");

            var matches = Regex.Match(restObject.Value<string>("id"), "/service/(?<servicename>[a-zA-Z0-9_-]*)/");
            string servicename = matches.Groups["servicename"].Value;



            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/{name}')]"  : $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/' ,parameters('{AddParameter($"property_{name}_name", "string", name)}'))]";
            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            AddParameterFromObject((JObject)resource["properties"], "value", secret ? "securestring" : "string", name);

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('APIM_servicename'))]");

            }
            resource["dependsOn"] = dependsOn;

            this.resources.Add(resource);
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
            

            var obj = new ResourceTemplate();
            obj.comments = "Generated for resource " + restObject.Value<string>("id");
            if (type == "Microsoft.ApiManagement/service/apis/policies")
            {
                var matches = Regex.Match(restObject.Value<string>("id"), "/service/(?<servicename>[a-zA-Z0-9_-]*)/apis/(?<apiname>[a-zA-Z0-9_-]*)");
                servicename = matches.Groups["servicename"].Value;
                apiname = matches.Groups["apiname"].Value;


                obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/{apiname}/{name}')]" : $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/' ,parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}'), '/' ,parameters('{AddParameter($"policy_{name}_name", "string", name)}'))]";
            }
            else if (type == "Microsoft.ApiManagement/service/apis/operations/policies")
            {
                var matches = Regex.Match(restObject.Value<string>("id"), "/service/(?<servicename>[a-zA-Z0-9_-]*)/apis/(?<apiname>[a-zA-Z0-9_-]*)/operations/(?<operationname>[a-zA-Z0-9_-]*)");
                servicename = matches.Groups["servicename"].Value;
                apiname = matches.Groups["apiname"].Value;
                operationname = matches.Groups["operationname"].Value;
                obj.name = parametrizePropertiesOnly ? $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/{apiname}/{operationname}/{name}')]" : $"[concat(parameters('{AddParameter($"APIM_servicename", "string", servicename)}'), '/' ,parameters('{AddParameter($"api_{apiname}_name", "string", apiname)}'), '/' ,parameters('{AddParameter($"operations_{operationname}_name", "string", operationname)}'), '/' ,parameters('{AddParameter($"policy_{name}_name", "string", name)}'))]";
            }

            obj.type = type;
            var resource = JObject.FromObject(obj);
            resource["properties"] = restObject["properties"];

            var dependsOn = new JArray();
            if (APIMInstanceAdded)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service', parameters('APIM_servicename'))]");

            }
            if (parametrizePropertiesOnly)
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('APIM_servicename') , '{apiname}')]");

                if (type == "Microsoft.ApiManagement/service/apis/operations/policies")
                {
                    dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis/operations', parameters('APIM_servicename'), '{apiname}', '{operationname}')]");
                }
            }
            else
            {
                dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis', parameters('APIM_servicename') ,parameters('api_{apiname}_name'))]");

                if (type == "Microsoft.ApiManagement/service/apis/operations/policies")
                {
                    dependsOn.Add($"[resourceId('Microsoft.ApiManagement/service/apis/operations', parameters('APIM_servicename'), parameters('api_{apiname}_name'), parameters('operations_{operationname}_name'))]");
                }
            }
            resource["dependsOn"] = dependsOn;
            return resource;
            //this.resources.Add(resource);
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
