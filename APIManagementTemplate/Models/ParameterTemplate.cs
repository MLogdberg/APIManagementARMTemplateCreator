using APIManagementTemplate;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicAppTemplate.Models;

public class ParameterTemplate
{
    [JsonProperty("$schema")]
    public string schema
    {
        get
        {
            return Constants.parameterSchema;
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

    public ParameterTemplate()
    {
        parameters = new JObject();
    }
}