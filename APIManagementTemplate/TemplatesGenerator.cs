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
        private const string StorageAccountResourceType = "Microsoft.Storage/storageAccounts";
        private const string SubscriptionResourceType = "Microsoft.ApiManagement/service/subscriptions";
        private const string UserResourceType = "Microsoft.ApiManagement/service/users";
        private const string GroupResourceType = "Microsoft.ApiManagement/service/groups";
        private const string UserGroupResourceType = "Microsoft.ApiManagement/service/groups/users";
        private const string OperationalInsightsWorkspaceResourceType = "Microsoft.OperationalInsights/workspaces";
        private const string AppInsightsResourceType = "Microsoft.Insights/components";
        private const string ProductPolicyResourceType = "Microsoft.ApiManagement/service/products/policies";

        public IList<GeneratedTemplate> Generate(string sourceTemplate, bool apiStandalone,
            bool separatePolicyFile = false)
        {
            JObject parsedTemplate = JObject.Parse(sourceTemplate);
            List<GeneratedTemplate> templates = GenerateAPIsAndVersionSets(apiStandalone, parsedTemplate);
            templates.AddRange(GenerateProducts(parsedTemplate, separatePolicyFile));
            templates.Add(GenerateTemplate(parsedTemplate, "service.template.json", String.Empty, ServiceResourceType,
                OperationalInsightsWorkspaceResourceType, AppInsightsResourceType, StorageAccountResourceType));
            templates.Add(GenerateTemplate(parsedTemplate, "subscriptions.template.json", String.Empty,
                SubscriptionResourceType));
            templates.Add(GenerateTemplate(parsedTemplate, "users.template.json", String.Empty, UserResourceType));
            templates.Add(GenerateTemplate(parsedTemplate, "groups.template.json", String.Empty, GroupResourceType));
            templates.Add(GenerateTemplate(parsedTemplate, "groupsUsers.template.json", String.Empty,
                UserGroupResourceType));
            return templates;
        }

        private List<GeneratedTemplate> GenerateAPIsAndVersionSets(bool apiStandalone, JObject parsedTemplate)
        {
            var apis = parsedTemplate["resources"].Where(rr => rr["type"].Value<string>() == ApiResourceType);
            List<GeneratedTemplate> templates =
                apis.Select(api => GenerateAPI(api, parsedTemplate, apiStandalone)).ToList();
            var versionSets = apis.Where(api => api["properties"]["apiVersionSetId"] != null)
                .Distinct(new ApiVersionSetIdComparer())
                .Select(api => GenerateVersionSet(api, parsedTemplate, apiStandalone)).ToList();
            templates.AddRange(versionSets);
            return templates;
        }

        private IEnumerable<GeneratedTemplate> GenerateProducts(JObject parsedTemplate, bool separatePolicyFile)
        {
            var products = parsedTemplate["resources"].Where(rr => rr["type"].Value<string>() == ProductResourceType);
            List<GeneratedTemplate> templates = products
                .Select(product => GenerateProduct(product, parsedTemplate, separatePolicyFile)).ToList();
            if (separatePolicyFile)
            {
                templates.AddRange(products.Select(p => GenerateProductPolicy(p)).Where(x => x != null));
            }
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
                policy["properties"]["contentFormat"] = "rawxml-link";
                policy["properties"]["policyContent"] = $"[concat(parameters('repoBaseUrl'), '/product-{productId}/product-{productId}.policy.xml', parameters('TemplatesStorageAccountSASToken'))]";
            }
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

        private GeneratedTemplate GenerateAPI(JToken api, JObject parsedTemplate, bool apiStandalone)
        {
            GeneratedTemplate generatedTemplate = new GeneratedTemplate();
            DeploymentTemplate template = new DeploymentTemplate(true);
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
            if (api["properties"]["apiVersionSetId"] != null)
            {
                string versionSetName = GetVersionSetName(api, parsedTemplate);
                string version = GetApiVersion(api, parsedTemplate);
                generatedTemplate.FileName = $"api-{versionSetName}.{version}.template.json";
                generatedTemplate.Directory = $@"api-{versionSetName}\{version}";
            }
            else
            {
                string name = api["properties"].Value<string>("displayName").Replace(' ', '-');
                generatedTemplate.FileName = $"api-{name}.template.json";
                generatedTemplate.Directory = $"api-{name}";
            }
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
            string[] split = jToken.Value<string>(name).Split(separator);
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