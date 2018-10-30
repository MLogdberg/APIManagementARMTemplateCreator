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
        [Parameter(Mandatory = false, HelpMessage = "Piped input from armclient", ValueFromPipeline = true)]
        public string ARMTemplate;

        [Parameter(Mandatory = false, HelpMessage = "Generate templates for APIs that can be deployed standalone (without the rest of the resources)")]
        public bool ApiStandalone = true;
        protected override void ProcessRecord()
        {
            var templates= new TemplatesGenerator().Generate(ARMTemplate, ApiStandalone);
            foreach (GeneratedTemplate template in templates)
            {
                string filename = template.FileName;
                if (!String.IsNullOrWhiteSpace(template.Directory))
                {
                    System.IO.Directory.CreateDirectory(template.Directory);
                    filename = $@"{template.Directory}\{template.FileName}";
                }
                System.IO.File.WriteAllText(filename, JObject.FromObject(template.Content).ToString());
            }
        }
    }
}
