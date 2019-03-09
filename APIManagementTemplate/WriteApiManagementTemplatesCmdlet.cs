using System;
using System.IO;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using APIManagementTemplate.Models;

namespace APIManagementTemplate
{
    [Cmdlet("Write", "APIManagementTemplates", ConfirmImpact = ConfirmImpact.None)]
    public class WriteApiManagementTemplatesCmdlet : PSCmdlet
    {
        [Parameter(Mandatory = false, HelpMessage = "Piped input from Get-APIManagementTemplate", ValueFromPipeline = true)]
        public string ARMTemplate;

        [Parameter(Mandatory = false, HelpMessage = "Generate templates for APIs that can be deployed standalone (without the rest of the resources)")]
        public bool ApiStandalone = true;

        [Parameter(Mandatory = false, HelpMessage = "If true only the names of the API will be added as array parameter")]
        public bool ListApiInProduct = false;

        [Parameter(Mandatory = false, HelpMessage = "The output directory")]
        public string OutputDirectory = ".";

        [Parameter(Mandatory = false, HelpMessage = "Policies are written to a separate file")]
        public bool SeparatePolicyFile = false;

        [Parameter(Mandatory = false, HelpMessage = "If the template already exists in the output directory, it will be merged with the new result.")]
        public bool MergeTemplates = false;
       
        [Parameter(Mandatory = false, HelpMessage = "If parameter files should be generated")]
        public bool GenerateParameterFiles = false;

        [Parameter(Mandatory = false, HelpMessage = "If the key to an Azure Function should be defined in a parameter instead of calling listsecrets")]
        public bool ReplaceListSecretsWithParameter = false;

        [Parameter(Mandatory = false, HelpMessage = "If set, the input template is written to this file ")]
        public string DebugTemplateFile = "";

        protected override void ProcessRecord()
        {
            if (!string.IsNullOrEmpty(DebugTemplateFile))
                File.WriteAllText(DebugTemplateFile, ARMTemplate);
            var templates= new TemplatesGenerator().Generate(ARMTemplate, ApiStandalone, SeparatePolicyFile, GenerateParameterFiles, ReplaceListSecretsWithParameter, ListApiInProduct);
            foreach (GeneratedTemplate template in templates)
            {
                string filename = $@"{OutputDirectory}\{template.FileName}";
                if (!String.IsNullOrWhiteSpace(template.Directory))
                {
                    var directory = $@"{OutputDirectory}\{template.Directory}";
                    Directory.CreateDirectory(directory);
                    filename = $@"{directory}\{template.FileName}";
                }

                if (File.Exists(filename) && MergeTemplates && template.Type == ContentType.Json)
                {
                    JObject oldTemplate = JObject.Parse(File.ReadAllText(filename));
                    template.Content = TemplateMerger.Merge(oldTemplate, template.Content);
                }
                File.WriteAllText(filename, template.Type == ContentType.Json ? template.Content.ToString() : template.XmlContent);
            }
        }
    }
}
