using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIManagementTemplate
{
    public interface IResourceCollector
    {
        string Login(string tenantName);
        Task<JObject> GetResource(string resourceId, string suffix = "", string apiversion = "2019-01-01");
        Task<JObject> GetResourceByURL(string url);
    }
}
