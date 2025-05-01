using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Test
{
    public class MockResourceCollector : IResourceCollector
    {
        private string basepath = "";
        public MockResourceCollector(string basepath)
        {
            this.basepath = basepath;
        }
        public Task<JObject> GetResource(string resourceId, string suffix = "", string apiversion = "2024-05-01")
        {
            var t = new Task<JObject>(() =>
            {
                var path = $"APIManagementTemplate.Test.Samples.{basepath}.{resourceId.Split('/').SkipWhile((a) => { return a != "service" && a != "workflows" && a != "sites"; }).Aggregate<string>((b, c) => { return b + "-" + c; })}.json";
                var resourceName = AzureResourceCollector.EscapeString(path);
                return JObject.Parse(Utils.GetEmbededFileContent(resourceName));
            });
            t.Start();
            return t;
        }

        public Task<JObject> GetResourceByURL(string url)
        {
            var t = new Task<JObject>(() =>
            {
                var uri = new Uri(url);
                var path = AzureResourceCollector.EscapeString($"APIManagementTemplate.Test.Samples.{basepath}.{uri.Host}{uri.AbsolutePath}");
                return JObject.Parse(Utils.GetEmbededFileContent(path));
            });
            t.Start();
            return t;
        }

        public string Login(string tenantName)
        {
            return "mocked";
        }
    }
}
