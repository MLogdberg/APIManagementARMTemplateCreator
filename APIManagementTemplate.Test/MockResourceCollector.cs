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
        public Task<JObject> GetResource(string resourceId, string suffix = "")
        {
            //should find in samples folders for reference
            return null;
        }

        public string Login(string tenantName)
        {
            return "mocked";        }
    }
}
