using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Models;

public class ResourceTemplate
{
    private List<string> names = new List<string>();

    public void AddName(string name)
    {
        names.Add(name);
    }

    public void RemoveNameAt(int i)
    {
        names.RemoveAt(i);
    }
    public string GetResourceId()
    {
        return String.Join(", ", names);
    }

    public string comments { get; set; }
    public string type { get; set; }
    public string name
    {
        get
        {
            if (string.IsNullOrEmpty(_name))
                return "[concat(" + String.Join(", '/', ", names) + ")]";
            return _name;
        }
        set
        {
            _name = value;
        }
    }

    private string _name = null;
    public string apiVersion { get; set; } = "2022-08-01";
    public JObject properties { get; set; }

    public IList<JObject> resources { get; set; }

    public JArray dependsOn { get; set; }

    public ResourceTemplate()
    {
        properties = new JObject();
        resources = new List<JObject>();
        dependsOn = new JArray();
    }
}