using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using APIManagementTemplate.Models;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate
{
    public class GeneratedTemplate
    {
        public string FileName { get; set; }
        public string Directory { get; set; }
        public JObject Content { get; set; }
        public string XmlContent { get; set; }
        public ContentType Type { get; set; } = ContentType.Json;

        public List<string> ExternalDependencies = new List<string>();

        public string GetPath()
        {
            return Directory == String.Empty ? $"/{FileName}" : $"/{Directory}/{FileName}";
        }
        public string GetUnixPath()
        {
            return GetPath().Replace(@"\", "/");
        }

        public string GetShortPath()
        {
            return String.IsNullOrWhiteSpace(Directory) ? FileName : $"{FileName}_{Directory.GetHashCode()}";
        }

    }

    public class FileInfo
    {
        public FileInfo(string fileName, string directory)
        {
            FileName = fileName;
            Directory = directory;
        }

        public string FileName { get; set; }
        public string Directory { get; set; }
    }

    public enum ContentType
    {
        Json,
        Xml
    }

    public class TemplatesGenerator
    {
        private const string ProductResourceType = "Microsoft.ApiManagement/service/products";
        private const string ApiResourceType = "Microsoft.ApiManagement/service/apis";
        private const string ServiceResourceType = "Microsoft.ApiManagement/service";
        private const string ServicePolicyResourceType = "Microsoft.ApiManagement/service/policies";
        private const string StorageAccountResourceType = "Microsoft.Storage/storageAccounts";
        private const string SubscriptionResourceType = "Microsoft.ApiManagement/service/subscriptions";
        private const string UserResourceType = "Microsoft.ApiManagement/service/users";
        private const string GroupResourceType = "Microsoft.ApiManagement/service/groups";
        private const string UserGroupResourceType = "Microsoft.ApiManagement/service/groups/users";
        private const string OperationalInsightsWorkspaceResourceType = "Microsoft.OperationalInsights/workspaces";
        private const string AppInsightsResourceType = "Microsoft.Insights/components";
        private const string ProductPolicyResourceType = "Microsoft.ApiManagement/service/products/policies";
        private const string ApiOperationResourceType = "Microsoft.ApiManagement/service/apis/operations";
        private const string ApiOperationPolicyResourceType = "Microsoft.ApiManagement/service/apis/operations/policies";
        private const string ApiPolicyResourceType = "Microsoft.ApiManagement/service/apis/policies";
        private const string ServicePolicyFileName = "service.policy.xml";
        private const string PropertyResourceType = "Microsoft.ApiManagement/service/properties";
        private const string BackendResourceType = "Microsoft.ApiManagement/service/backends";
        private const string OpenIdConnectProviderResourceType = "Microsoft.ApiManagement/service/openidConnectProviders";
        private const string CertificateResourceType = "Microsoft.ApiManagement/service/certificates";

        public IList<GeneratedTemplate> Generate(string sourceTemplate, bool apiStandalone, bool separatePolicyFile = false)
        {
            JObject parsedTemplate = JObject.Parse(sourceTemplate);
            List<GeneratedTemplate> templates = GenerateAPIsAndVersionSets(apiStandalone, parsedTemplate, separatePolicyFile);
            templates.AddRange(GenerateProducts(parsedTemplate, separatePolicyFile));
            templates.AddRange(GenerateService(parsedTemplate, separatePolicyFile));
            templates.Add(GenerateTemplate(parsedTemplate, "subscriptions.template.json", String.Empty, SubscriptionResourceType));
            templates.Add(GenerateTemplate(parsedTemplate, "users.template.json", String.Empty, UserResourceType));
            templates.Add(GenerateTemplate(parsedTemplate, "groups.template.json", String.Empty, GroupResourceType));
            templates.Add(GenerateTemplate(parsedTemplate, "groupsUsers.template.json", String.Empty,
                UserGroupResourceType));
            MoveExternalDependencies(templates);
            templates.Add(GenerateMasterTemplate(templates.Where(x => x.Type == ContentType.Json).ToList(), parsedTemplate, separatePolicyFile));
            return templates;
        }

        private void MoveExternalDependencies(List<GeneratedTemplate> templates)
        {
            foreach (GeneratedTemplate template in templates.Where(x => x.Type == ContentType.Json))
            {
                var dependsOn = template.Content.SelectTokens("$..dependsOn[*]").ToList();
                foreach (JToken dependency in dependsOn)
                {
                    var name = dependency.Value<string>();
                    if (!IsLocalDependency(name, template))
                    {
                        template.ExternalDependencies.Add(name);
                        dependency.Remove();
                    }
                }
            }
        }

        private static bool IsLocalDependency(string name, GeneratedTemplate template)
        {
            var resourceType = GetSplitPart(1, name);
            var nameParts = name.Split(',').Skip(1).Select(x => x.Trim().Replace("'))]", "')").Replace("')]", "')"));
            var localDependency = template.Content.SelectTokens($"$..resources[?(@.type=='{resourceType}')]")
                .Any(resource => nameParts.All(namePart => NameContainsPart(resource, namePart)));
            return localDependency;
        }

        private static bool NameContainsPart(JToken resource, string namePart)
        {
            string name = resource.Value<string>("name").ToLowerInvariant();
            string part = namePart.ToLowerInvariant();
            return name.Contains(part) || (part.StartsWith("'") && name.Contains($"'/{part.Split('\'')[1]}'"));
        }

        private GeneratedTemplate GenerateMasterTemplate(List<GeneratedTemplate> generatedTemplates, JObject parsedTemplate, bool separatePolicyFile)
        {
            var generatedTemplate = new GeneratedTemplate { Directory = String.Empty, FileName = "master.template.json" };
            DeploymentTemplate template = new DeploymentTemplate(true);
            foreach (GeneratedTemplate template2 in generatedTemplates)
            {
                template.resources.Add(GenerateDeployment(template2, generatedTemplates));
            }
            template.parameters = GetParameters(parsedTemplate["parameters"], template.resources, separatePolicyFile);
            generatedTemplate.Content = JObject.FromObject(template);
            return generatedTemplate;
        }

        private JObject GetParameters(JToken parameters, IList<JObject> resources, bool separatePolicyFile)
        {
            var p = resources.Select(x => GetParameters(parameters, x)).ToArray();
            var allParameters = p[0];
            foreach (JObject jObject in p.Skip(1))
            {
                allParameters.Merge(jObject);
            }
            if (!separatePolicyFile)
            {
                if (allParameters["repoBaseUrl"] == null)
                {
                    allParameters["repoBaseUrl"] = JToken.FromObject(new
                    {
                        type = "string",
                        metadata = new {description = "Base URL of the repository"}
                    });
                }
                if (allParameters["TemplatesStorageAccountSASToken"] == null)
                {
                    allParameters["TemplatesStorageAccountSASToken"] = JToken.FromObject(new
                    {
                        type = "string",
                        defaultValue = String.Empty
                    });
                }
            }
            return allParameters;
        }

        private JObject GenerateDeployment(GeneratedTemplate template2, List<GeneratedTemplate> generatedTemplates)
        {
            var deployment = new
            {
                apiVersion = "2017-05-10",
                name = template2.GetShortPath(),
                type ="Microsoft.Resources/deployments",
                properties = new {
                    mode = "Incremental",
                    templateLink = new
                    {
                        uri = $"[concat(parameters('repoBaseUrl'), '{template2.GetUnixPath()}', parameters('TemplatesStorageAccountSASToken'))]",
                        contentVersion = "1.0.0.0"
                    },
                    parameters = GenerateDeploymentParameters(template2)
                },
                dependsOn = GenerateDeploymentDependsOn(template2, generatedTemplates)
            };

            return JObject.FromObject(deployment);
        }


        private JArray GenerateDeploymentDependsOn(GeneratedTemplate template, List<GeneratedTemplate> generatedTemplates)
        {
            var dependsOn = new JArray();
            foreach (string name in template.ExternalDependencies)
            {
                var matches = generatedTemplates.Where(t => IsLocalDependency(name, t));
                if(matches.Any())
                {
                    var match = matches.First();
                    dependsOn.Add($"[resourceId('Microsoft.Resources/deployments', '{match.GetShortPath()}')]");
                }
                else
                {
                    var notFound = true;
                }
            }
            return dependsOn;
        }

        private JObject GenerateDeploymentParameters(GeneratedTemplate template2)
        {
            var parameters = new JObject();
            var parametersFromTemplate = template2.Content["parameters"];
            foreach (JProperty token in parametersFromTemplate.Cast<JProperty>())
            {
                var name = token.Name;
                parameters.Add(name, JObject.FromObject(new {value = $"[parameters('{name}')]"}));
            }
            return parameters;
        }

        private IEnumerable<GeneratedTemplate> GenerateService(JObject parsedTemplate, bool separatePolicyFile)
        {
            List<GeneratedTemplate> templates = new List<GeneratedTemplate>();
            string[] wantedResources = {
                ServiceResourceType,OperationalInsightsWorkspaceResourceType, AppInsightsResourceType, StorageAccountResourceType
            };
            var generatedTemplate = new GeneratedTemplate { FileName = "service.template.json", Directory = String.Empty };
            DeploymentTemplate template = new DeploymentTemplate(true);
            var resources = parsedTemplate.SelectTokens("$.resources[*]")
                .Where(r => wantedResources.Any(w => w == r.Value<string>("type")));
            foreach (JToken resource in resources)
            {
                if (resource.Value<string>("type") == ServiceResourceType)
                {
                    AddServiceResources(parsedTemplate, resource, PropertyResourceType);
                    AddServiceResources(parsedTemplate, resource, BackendResourceType);
                    AddServiceResources(parsedTemplate, resource, OpenIdConnectProviderResourceType);
                    AddServiceResources(parsedTemplate, resource, CertificateResourceType);
                    if (separatePolicyFile)
                    {
                        var policy = resource.SelectToken($"$..resources[?(@.type==\'{ServicePolicyResourceType}\')]");
                        if (policy != null)
                        {
                            templates.Add(GenerateServicePolicyFile(parsedTemplate, policy));
                            ReplacePolicyWithFileLink(policy, new FileInfo(ServicePolicyFileName, String.Empty));
                        }
                    }
                }
                template.parameters = GetParameters(parsedTemplate["parameters"], resource);
                template.resources.Add(JObject.FromObject(resource));
            }
            generatedTemplate.Content = JObject.FromObject(template);
            templates.Add(generatedTemplate);
            return templates;
        }

        private static void AddServiceResources(JObject parsedTemplate, JToken resource, string resourceType)
        {
            var properties = parsedTemplate.SelectTokens($"$..resources[?(@.type==\'{resourceType}\')]");
            JArray subResources = (JArray) resource["resources"];
            foreach (JToken property in properties.ToArray())
            {
                subResources.Add(property);
            }
        }

        private List<GeneratedTemplate> GenerateAPIsAndVersionSets(bool apiStandalone, JObject parsedTemplate, bool separatePolicyFile)
        {
            var apis = parsedTemplate["resources"].Where(rr => rr["type"].Value<string>() == ApiResourceType);
            List<GeneratedTemplate> templates = separatePolicyFile? GenerateAPIPolicyFiles(apis, parsedTemplate).ToList()
                : new List<GeneratedTemplate>();
            templates.AddRange(apis.Select(api => GenerateAPI(api, parsedTemplate, apiStandalone, separatePolicyFile)));
            var versionSets = apis.Where(api => api["properties"]["apiVersionSetId"] != null)
                .Distinct(new ApiVersionSetIdComparer())
                .Select(api => GenerateVersionSet(api, parsedTemplate, apiStandalone)).ToList();
            templates.AddRange(versionSets);
            return templates;
        }

        private IEnumerable<GeneratedTemplate> GenerateAPIPolicyFiles(IEnumerable<JToken> apis, JObject parsedTemplate)
        {
            var policyFiles = new List<GeneratedTemplate>();
            foreach (JToken api in apis)
            {
                var apiPolicy = api["resources"].FirstOrDefault(x => x.Value<string>("type") == ApiPolicyResourceType);
                if (apiPolicy != null)
                {
                    policyFiles.Add(GeneratePolicyFile(parsedTemplate, apiPolicy, api, String.Empty));
                }
                var operations = api["resources"].Where(x => x.Value<string>("type") == ApiOperationResourceType);
                foreach (var operation in operations)
                {
                    var policy = operation["resources"].FirstOrDefault(x => x.Value<string>("type") == ApiOperationPolicyResourceType);
                    if (policy != null)
                    {
                        var operationId = GetParameterPart(operation, "name", -2);
                        policyFiles.Add(GeneratePolicyFile(parsedTemplate, policy, api, operationId));
                    }
                }
            }
            return policyFiles;
        }

        private static GeneratedTemplate GeneratePolicyFile(JObject parsedTemplate, JToken policy, JToken api,
            string operationId)
        {
            var content = policy["properties"].Value<string>("policyContent");
            var template = new GeneratedTemplate
            {
                Type = ContentType.Xml,
                XmlContent = content
            };
            var filenameAndDirectory = GetFilenameAndDirectoryForOperationPolicy(api, parsedTemplate, operationId);
            template.FileName = filenameAndDirectory.FileName;
            template.Directory = filenameAndDirectory.Directory;
            return template;
        }

        private static GeneratedTemplate GenerateServicePolicyFile(JObject parsedTemplate, JToken policy)
        {
            var content = policy["properties"].Value<string>("policyContent");
            var template = new GeneratedTemplate
            {
                Type = ContentType.Xml,
                XmlContent = content,
                FileName = ServicePolicyFileName,
                Directory = String.Empty
            };
            return template;
        }

        private IEnumerable<GeneratedTemplate> GenerateProducts(JObject parsedTemplate, bool separatePolicyFile)
        {
            var products = parsedTemplate["resources"].Where(rr => rr["type"].Value<string>() == ProductResourceType);
            List<GeneratedTemplate> templates = new List<GeneratedTemplate>();
            if (separatePolicyFile)
            {
                templates.AddRange(products.Select(p => GenerateProductPolicy(p)).Where(x => x != null));
            }
            templates.AddRange(products
                .Select(product => GenerateProduct(product, parsedTemplate, separatePolicyFile)));
            return templates;
        }

        private GeneratedTemplate GenerateProductPolicy(JToken product)
        {
            var productId = GetParameterPart(product, "name", -2).Substring(1);
            var policy = product["resources"]
                .FirstOrDefault(rr => rr["type"].Value<string>() == ProductPolicyResourceType);
            if (policy?["properties"] == null)
                return null;
            var content = policy["properties"].Value<string>("policyContent");
            return new GeneratedTemplate
            {
                Directory = $"product-{productId}",
                FileName = $"product-{productId}.policy.xml",
                Type = ContentType.Xml,
                XmlContent = content
            };
        }

        private GeneratedTemplate GenerateProduct(JToken product, JObject parsedTemplate, bool separatePolicyFile)
        {
            var productId = GetParameterPart(product, "name", -2).Substring(1);
            GeneratedTemplate generatedTemplate = new GeneratedTemplate
            {
                Directory = $"product-{productId}",
                FileName = $"product-{productId}.template.json"
            };
            DeploymentTemplate template = new DeploymentTemplate(true);
            if (separatePolicyFile)
            {
                ReplaceProductPolicyWithFileLink(product, productId);
                AddParametersForFileLink(parsedTemplate);
            }
            template.parameters = GetParameters(parsedTemplate["parameters"], product);
            template.resources.Add(JObject.FromObject(product));
            generatedTemplate.Content = JObject.FromObject(template);
            return generatedTemplate;
        }

        private void AddParametersForFileLink(JToken template)
        {
            var parameters = template["parameters"];
            if (parameters != null)
            {
                parameters["repoBaseUrl"] = JToken.FromObject(new
                {
                    type = "string",
                    metadata = new {description = "Base URL of the repository"}
                });
                parameters["TemplatesStorageAccountSASToken"] = JToken.FromObject(new
                {
                    type = "string",
                    defaultValue = String.Empty
                });
            }
        }

        private void ReplaceProductPolicyWithFileLink(JToken product, string productId)
        {
            var policy = product["resources"].FirstOrDefault(x => x.Value<string>("type") == ProductPolicyResourceType);
            if (policy != null)
            {
                policy["apiVersion"] = "2018-01-01";
                policy["properties"]["contentFormat"] = "rawxml-link";
                policy["properties"]["policyContent"] = $"[concat(parameters('repoBaseUrl'), '/product-{productId}/product-{productId}.policy.xml', parameters('TemplatesStorageAccountSASToken'))]";
            }
        }
        private void ReplaceApiOperationPolictPolicyWithFileLink(JToken api, JObject parsedTemplate)
        {
            ReplacePoliciesWithFileLink(api, ApiOperationPolicyResourceType, policy => GetParameterPart(policy, "name", -6), parsedTemplate);
            ReplacePoliciesWithFileLink(api, ApiPolicyResourceType, policy => String.Empty, parsedTemplate);
        }

        private static void ReplacePoliciesWithFileLink(JToken api, string policyResourceType, Func<JToken, string> operationIdFunc, JObject parsedTemplate)
        {
            var jpath = $"$..resources[?(@.type==\'{policyResourceType}\')]";
            var policies = api.SelectTokens(jpath);
            foreach (JToken policy in policies)
            {
                var operationId = operationIdFunc(policy);
                ReplacePolicyWithFileLink(api, parsedTemplate, operationId, policy);
            }
        }

        private static void ReplacePolicyWithFileLink(JToken api, JObject parsedTemplate, string operationId, JToken policy)
        {
            var fileInfo = GetFilenameAndDirectoryForOperationPolicy(api, parsedTemplate, operationId);
            ReplacePolicyWithFileLink(policy, fileInfo);
        }

        private static void ReplacePolicyWithFileLink(JToken policy, FileInfo fileInfo)
        {
            policy["properties"]["contentFormat"] = "rawxml-link";
            policy["apiVersion"] = "2018-01-01";
            string formattedDirectory = fileInfo.Directory.Replace(@"\", "/");
            var directory = $"/{formattedDirectory}";
            if (directory == "/")
                directory = String.Empty;
            policy["properties"]["policyContent"] =
                $"[concat(parameters('repoBaseUrl'), '{directory}/{fileInfo.FileName}', parameters('TemplatesStorageAccountSASToken'))]";
        }

        private static GeneratedTemplate GenerateTemplate(JObject parsedTemplate, string filename, string directory,
            params string[] wantedResources)
        {
            var generatedTemplate = new GeneratedTemplate {Directory = directory, FileName = filename};
            DeploymentTemplate template = new DeploymentTemplate(true);
            var resources = parsedTemplate.SelectTokens("$.resources[*]")
                .Where(r => wantedResources.Any(w => w == r.Value<string>("type")));
            foreach (JToken resource in resources)
            {
                template.parameters = GetParameters(parsedTemplate["parameters"], resource);
                template.resources.Add(JObject.FromObject(resource));
            }
            generatedTemplate.Content = JObject.FromObject(template);
            return generatedTemplate;
        }

        private GeneratedTemplate GenerateVersionSet(JToken api, JObject parsedTemplate, bool apiStandalone)
        {
            GeneratedTemplate generatedTemplate = new GeneratedTemplate();
            DeploymentTemplate template = new DeploymentTemplate(true);
            SetFilenameAndDirectoryForVersionSet(api, generatedTemplate, parsedTemplate);
            var versionSetId = GetParameterPart(api["properties"], "apiVersionSetId", -2);
            var versionSet = parsedTemplate
                .SelectTokens("$.resources[?(@.type==\'Microsoft.ApiManagement/service/api-version-sets\')]")
                .FirstOrDefault(x => x.Value<string>("name").Contains(versionSetId));
            if (versionSet != null)
            {
                template.parameters = GetParameters(parsedTemplate["parameters"], versionSet);
                template.resources.Add(apiStandalone
                    ? RemoveServiceDependencies(versionSet)
                    : JObject.FromObject(versionSet));
            }
            generatedTemplate.Content = JObject.FromObject(template);
            return generatedTemplate;
        }

        private GeneratedTemplate GenerateAPI(JToken api, JObject parsedTemplate, bool apiStandalone, bool separatePolicyFile)
        {
            GeneratedTemplate generatedTemplate = new GeneratedTemplate();
            DeploymentTemplate template = new DeploymentTemplate(true);
            if (separatePolicyFile)
            {
                ReplaceApiOperationPolictPolicyWithFileLink(api, parsedTemplate);
                AddParametersForFileLink(parsedTemplate);
            }
            template.parameters = GetParameters(parsedTemplate["parameters"], api);

            SetFilenameAndDirectory(api, parsedTemplate, generatedTemplate);
            template.resources.Add(apiStandalone ? RemoveServiceDependencies(api) : JObject.FromObject(api));
            generatedTemplate.Content = JObject.FromObject(template);
            return generatedTemplate;
        }

        private static JObject RemoveServiceDependencies(JToken api)
        {
            JObject item = JObject.FromObject(api);
            var dependsOn = item.SelectTokens("$..dependsOn[*]").Where(token =>
                token.Value<string>().StartsWith("[resourceId('Microsoft.ApiManagement/service'")).ToList();
            dependsOn.ForEach(token => token.Remove());
            return item;
        }

        private static void SetFilenameAndDirectory(JToken api, JObject parsedTemplate,
            GeneratedTemplate generatedTemplate)
        {
            var filenameAndDirectory = GetFileNameAndDirectory(api, parsedTemplate);
            generatedTemplate.FileName = filenameAndDirectory.FileName;
            generatedTemplate.Directory = filenameAndDirectory.Directory;
        }

        private static FileInfo GetFilenameAndDirectoryForOperationPolicy(JToken api,
            JObject parsedTemplate, string operationId)
        {
            var fileInfo = GetFileNameAndDirectory(api, parsedTemplate);
            fileInfo.FileName = fileInfo.FileName.Replace(".template.json", 
                String.IsNullOrWhiteSpace(operationId) ? ".policy.xml" : $".{operationId}.policy.xml");
            return fileInfo;
        }

        private static FileInfo GetFileNameAndDirectory(JToken api, JObject parsedTemplate)
        {
            string filename, directory;
            if (api["properties"]["apiVersionSetId"] != null)
            {
                string versionSetName = GetVersionSetName(api, parsedTemplate);
                string version = GetApiVersion(api, parsedTemplate);
                filename = $"api-{versionSetName}.{version}.template.json";
                directory = $@"api-{versionSetName}\{version}";
                return new FileInfo(filename, directory);
            }
            string name = api["properties"].Value<string>("displayName").Replace(' ', '-');
            filename = $"api-{name}.template.json";
            directory = $"api-{name}";
            return new FileInfo(filename, directory);
        }

        private static void SetFilenameAndDirectoryForVersionSet(JToken api, GeneratedTemplate generatedTemplate,
            JObject parsedTemplate)
        {
            string versionSetName = GetVersionSetName(api, parsedTemplate);
            generatedTemplate.FileName = $"api-{versionSetName}.version-set.template.json";
            generatedTemplate.Directory = $@"api-{versionSetName}";
        }

        private static string GetApiVersion(JToken api, JObject parsedTemplate)
        {
            var apiVersion = GetParameterPart(api["properties"], "apiVersion", -2);
            var jpath = $"$.parameters.{apiVersion}.defaultValue";
            var version = parsedTemplate.SelectToken(jpath).Value<string>();
            return version;
        }

        private static string GetVersionSetName(JToken api, JObject parsedTemplate)
        {
            JToken apivs = GetVersionSet(api, parsedTemplate);
            var versionSetName = apivs["properties"].Value<string>("displayName");
            var formattedVersionSetName = versionSetName.Replace(' ', '-');
            return formattedVersionSetName;
        }

        private static JToken GetVersionSet(JToken api, JObject parsedTemplate)
        {
            var versionSetId = GetParameterPart(api["properties"], "apiVersionSetId", -2);
            var apivs = parsedTemplate
                .SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/api-version-sets')]")
                .FirstOrDefault(x => x.Value<string>("name").Contains(versionSetId));
            return apivs;
        }

        private static string GetParameterPart(JToken jToken, string name, int index, char separator = '\'')
        {
            var value = jToken.Value<string>(name);
            return GetSplitPart(index, value, separator);
        }

        private static string GetSplitPart(int index, string value, char separator = '\'')
        {
            string[] split = value.Split(separator);
            if (index > split.Length - 1 || index < -1 * split.Length)
                return String.Empty;
            if (index >= 0)
                return split[index];
            var length = split.Length;
            return split[length + index];
        }

        private static JObject GetParameters(JToken parameters, JToken api)
        {
            var regExp = new Regex("parameters\\('(?<parameter>.+?)'\\)");
            MatchCollection matches = regExp.Matches(api.ToString());
            IEnumerable<string> usedParameters =
                matches.Cast<Match>().Select(x => x.Groups["parameter"].Value).Distinct();
            IEnumerable<JProperty> filteredParameters =
                parameters.Cast<JProperty>().Where(x => usedParameters.Contains(x.Name));
            return new JObject(filteredParameters);
        }
    }
}