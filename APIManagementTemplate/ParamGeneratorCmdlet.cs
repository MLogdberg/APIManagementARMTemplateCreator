using LogicAppTemplate.Models;
using Newtonsoft.Json.Linq;
using System.Dynamic;
using System.Management.Automation;

namespace APIManagementTemplate;

[Cmdlet(VerbsCommon.Get, "ParameterTemplate", ConfirmImpact = ConfirmImpact.None)]
public class ParamGenerator : PSCmdlet
{
    [Parameter(
        Mandatory = true,
        HelpMessage = "The path to the template file"
    )]
    public string TemplateFile;

    [Parameter(
        Mandatory = false,
        HelpMessage = "How to handle KeyVault integration, default is None, available options None or Static, Static will generate parameters for static reference to KeyVault"
    )]
    public KeyVaultUsage KeyVault = KeyVaultUsage.None;

    public enum KeyVaultUsage
    {
        None,
        Static
    }

    private ParameterTemplate paramTemplate;
    public ParamGenerator()
    {
        /*
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceName = "LogicAppTemplate.Templates.paramTemplate.json";
        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        using (StreamReader reader = new StreamReader(stream))
        {
            paramTemplate = JsonConvert.DeserializeObject<ParameterTemplate>(reader.ReadToEnd());
        }*/
        paramTemplate = new ParameterTemplate();

    }

    protected override void ProcessRecord()
    {
        var path = System.IO.Path.GetFullPath(TemplateFile);
        var logicappTemplate = JObject.Parse(File.ReadAllText(path));
        var result = CreateParameterFileFromTemplate(logicappTemplate);


        WriteObject(result.ToString());
    }

    public JObject CreateParameterFileFromTemplate(JObject logicAppTemplate)
    {
        foreach (var param in logicAppTemplate["parameters"].Children<JProperty>())
        {
            // Don't create parameters that reference a ARM Template expression
            if (param.Value.Value<string>("type").Equals("string", StringComparison.CurrentCultureIgnoreCase) && param.Value.Value<string>("defaultValue") != null && param.Value.Value<string>("defaultValue").StartsWith("["))
            {
                continue;
            }

            var obj = new JObject();
            if (KeyVaultUsage.Static == KeyVault && (string)logicAppTemplate["parameters"][param.Name]["type"] == "securestring")
            {
                dynamic k = new ExpandoObject();
                k.keyVault = new ExpandoObject();
                k.keyVault.id = "/subscriptions/{subscriptionid}/resourceGroups/{resourcegroupname}/providers/Microsoft.KeyVault/vaults/{vault-name}";
                k.secretName = param.Name;
                obj["reference"] = JObject.FromObject(k);
            }
            else
            {
                obj["value"] = logicAppTemplate["parameters"][param.Name]["defaultValue"];
            }

            paramTemplate.parameters.Add(param.Name, obj);
        }

        return JObject.FromObject(paramTemplate);
    }
}