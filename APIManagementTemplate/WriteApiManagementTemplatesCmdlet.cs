using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using APIManagementTemplate.Models;

namespace APIManagementTemplate
{
    [Cmdlet("Write", "APIManagementTemplates", ConfirmImpact = ConfirmImpact.None)]
    public class WriteApiManagementTemplatesCmdlet : PSCmdlet
    {
        [Parameter(Mandatory = false, HelpMessage = "Piped input from armclient", ValueFromPipeline = true)]
        public string ARMTemplate;

        protected override void ProcessRecord()
        {
            var templates= new TemplatesGenerator().Generate(ARMTemplate);
            foreach (GeneratedTemplate template in templates)
            {
                System.IO.File.WriteAllText(template.FileName, JObject.FromObject(template.Content).ToString());
            }
        }

        private static JObject RemoveDependencyToService(JToken api)
        {
            JObject item = JObject.FromObject(api);
            var dependsOn = item.SelectTokens("$..dependsOn[*]");
            foreach (JToken token in dependsOn.ToArray())
            {
                var dependency = token.Value<string>();
                var list = new[]
                {
                    "[resourceId('Microsoft.ApiManagement/service'"
                };
                if (list.Any(x => dependency.StartsWith(x)))
                    token.Remove();
            }
            return item;
        }

        private JObject GetApiVersionSet(JObject parsedTemplate, JToken versionSetId)
        {
            var id = versionSetId.Value<string>().Split(',')[2];
            var apiVersionSets = parsedTemplate.SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/api-version-sets')]");
            var apiVersionSet = apiVersionSets.First(x => x.Value<string>("name").Contains(id));
            return RemoveDependencyToService(apiVersionSet);
        }

        private static JObject GetParameters(JToken parameters, JToken api)
        {
            var regExp = new Regex("parameters\\('(?<parameter>.+?)'\\)");
            MatchCollection matches = regExp.Matches(api.ToString());
            IEnumerable<string> usedParameters = matches.Cast<Match>().Select(x => x.Groups["parameter"].Value).Distinct();
            IEnumerable<JProperty> filteredParameters = parameters.Cast<JProperty>().Where(x => usedParameters.Contains(x.Name));
            return new JObject(filteredParameters);
        }

        private static string GetFileName(JToken api)
        {
            string filename;
            if (api["properties"]["apiVersion"] != null)
            {
                filename = api["properties"].Value<string>("apiVersion").Split('\'')[1] + ".json";
            }
            else
            {
                filename= $"{api["properties"].Value<string>("path")}.json";
            }
            return filename.Replace("_apiVersion", String.Empty);

        }
    }
}
