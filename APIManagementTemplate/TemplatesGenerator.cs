using System;
using System.Collections.Generic;
using System.Linq;
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
    }

    public class TemplatesGenerator
    {
        public IList<GeneratedTemplate> Generate(string sourceTemplate)
        {
            JObject parsedTemplate = JObject.Parse(sourceTemplate);

            var apis = parsedTemplate["resources"].Where(rr => rr["type"].Value<string>() == "Microsoft.ApiManagement/service/apis");
            return apis.Select(api => GenerateAPI(api, parsedTemplate)).ToList();

            //var apivs = parsedTemplate.SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/api-version-sets')]");
            //var parameters = parsedTemplate["parameters"];
            //foreach (JToken api in apis)
            //{
            //    DeploymentTemplate template = new DeploymentTemplate(true);
            //    JObject item = RemoveDependencyToService(api);
            //    template.resources.Add(item);
            //    var apiVersionSetId = api["properties"]["apiVersionSetId"];
            //    if (apiVersionSetId != null)
            //    {
            //        template.resources.Add(GetApiVersionSet(parsedTemplate, apiVersionSetId));
            //    }
            //    template.parameters = GetParameters(parameters, api);
            //    var filename = GetFileName(api);
            //    System.IO.File.WriteAllText(filename, JObject.FromObject(template).ToString());
            //}

            //return new List<GeneratedTemplate>();
        }

        private GeneratedTemplate GenerateAPI(JToken api, JObject parsedTemplate)
        {
            GeneratedTemplate generatedTemplate = new GeneratedTemplate();
            DeploymentTemplate template = new DeploymentTemplate(true);
            template.resources.Add(JObject.FromObject(api));
            generatedTemplate.Content= JObject.FromObject(template);
            SetFilenameAndDirectory(api, parsedTemplate, generatedTemplate);
            return generatedTemplate;
        }

        private static void SetFilenameAndDirectory(JToken api, JObject parsedTemplate, GeneratedTemplate generatedTemplate)
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

        private static string GetApiVersion(JToken api, JObject parsedTemplate)
        {
            var apiVersion = GetParameterPart(api["properties"], "apiVersion", -1);
            var jpath = $"$.parameters.{apiVersion}.defaultValue";
            var version = parsedTemplate.SelectToken(jpath).Value<string>();
            return version;
        }

        private static string GetVersionSetName(JToken api, JObject parsedTemplate)
        {
            var versionSetId = GetParameterPart(api["properties"], "apiVersionSetId", -1);
            var apivs = parsedTemplate
                .SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/api-version-sets')]")
                .FirstOrDefault(x => x.Value<string>("name").Contains(versionSetId));
            var versionSetName = apivs["properties"].Value<string>("displayName");
            var formattedVersionSetName = versionSetName.Replace(' ', '-');
            return formattedVersionSetName;
        }

        private string GetFileName(JToken api, JObject parsedTemplate)
        {
            string filename;
            var nameValue = api["name"].Value<string>();
            string[] split = nameValue.Split('\'');
            var name = split[split.Length-2].Substring(1);
            //return $"{name}.json";
            

            if (api["properties"]["apiVersionSetId"] != null)
            {
                var apiVersion = GetParameterPart(api["properties"], "apiVersion", -1);
                var versionSetId = GetParameterPart(api["properties"], "apiVersionSetId", -1);
                var apivs = parsedTemplate
                    .SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/api-version-sets')]")
                    .FirstOrDefault(x => x.Value<string>("name").Contains(versionSetId));
                var versionSetName = apivs["properties"].Value<string>("displayName");
                var formattedVersionSetName = versionSetName.Replace(' ', '-');
                var jpath = $"$.parameters.{apiVersion}.defaultValue";
                var version = parsedTemplate.SelectToken(jpath).Value<string>();
                return $"api-{formattedVersionSetName}.{version}.template.json";
            }
            return $"api-{name}.template.json";
        }

        private static string GetParameterPart(JToken jToken, string name, int index, char separator= '\'')
        {
            string[] split = jToken.Value<string>(name).Split(separator);
            if(index >= 0)
                return split[index];
            var length = split.Length;
            return split[length -1 + index];
        }
    }
}