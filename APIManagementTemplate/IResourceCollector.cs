using Newtonsoft.Json.Linq;

namespace APIManagementTemplate;

public interface IResourceCollector
{
    string Login(string tenantName);
    Task<JObject> GetResource(string resourceId, string suffix = "", string apiversion = "2022-08-01");
    Task<JObject> GetResourceByURL(string url);
}