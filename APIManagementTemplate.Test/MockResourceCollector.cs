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
        public Task<JObject> GetResource(string resourceId, string suffix = "")
        {
            var t = new Task<JObject>(() => { return JObject.Parse(Utils.GetEmbededFileContent($"APIManagementTemplate.Test.Samples.{basepath}.{resourceId.Split('/').SkipWhile((a) => { return a != "service"; }).Aggregate<string>((b, c) => { return b + "-" + c; })}.json")); });
            t.Start();
            return t;
        }

        public string Login(string tenantName)
        {
            return "mocked";        }
    }
}
