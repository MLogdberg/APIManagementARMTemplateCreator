using System;
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

        [Parameter(Mandatory = false, HelpMessage = "The output directory")]
        public string OutputDirectory = ".";

        [Parameter(Mandatory = false, HelpMessage = "Policies are written to a separate file")]
        public bool SeparatePolicyFile = false;

        [Parameter(Mandatory = false, HelpMessage = "If set, the input template is written to this file ")]
        public string DebugTemplateFile = "";

        protected override void ProcessRecord()
        {
            if (!string.IsNullOrEmpty(DebugTemplateFile))
                System.IO.File.WriteAllText(DebugTemplateFile, ARMTemplate);
            var templates= new TemplatesGenerator().Generate(ARMTemplate, ApiStandalone, SeparatePolicyFile);
            foreach (GeneratedTemplate template in templates)
            {
                string filename = $@"{OutputDirectory}\{template.FileName}";
                if (!String.IsNullOrWhiteSpace(template.Directory))
                {
                    var directory = $@"{OutputDirectory}\{template.Directory}";
                    System.IO.Directory.CreateDirectory(directory);
                    filename = $@"{directory}\{template.FileName}";
                }
                System.IO.File.WriteAllText(filename, template.Type == ContentType.Json ? template.Content.ToString() : template.XmlContent);
            }
        }
    }
}
