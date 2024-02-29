using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Models;

public class DeploymentParameters
{

    [JsonProperty("$schema")]
    public string schema => Constants.deploymenParameterSchema;

    public string contentVersion => "1.0.0.0";

    public JObject parameters { get; set; }


    public DeploymentParameters()
    {
        parameters = new JObject();
    }

    public static DeploymentParameters FromString(string template)
    {
        return JsonConvert.DeserializeObject<DeploymentParameters>(template);
    }


    public void AddParameter(string name, object value)
    {
        JObject param = new JObject();
        param.Add("value", JToken.FromObject(value));
        parameters.Add(name, param);
    }


    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}