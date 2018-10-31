using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class WithoutApiVersionSetIdTests
    {
        private IResourceCollector collector;
        [TestInitialize()]
        public void Initialize()
        {
            this.collector = new MockResourceCollector("WithoutApiVersionSetId");

        }
        private JObject _template = null;
        private JObject GetTemplate(bool exportProducts = false)
        {
            if (this._template != null)
                return this._template;
            var generator = new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest", "maloapimtestclean", false, exportProducts, false, false, this.collector);
            this._template = generator.GenerateTemplate().GetAwaiter().GetResult();
            return this._template;
        }



        [TestMethod]
        public void TestApiVersionSetIdForApiIsNotSet()
        {
            var template = GetTemplate(true);
            var api = template["resources"].FirstOrDefault(rr => rr["type"].Value<string>() == "Microsoft.ApiManagement/service/apis");
            Assert.IsNotNull(api);

            var versionSetId = api["properties"]["apiVersionSetId"];
            Assert.IsNull(versionSetId);
        }
    }
}
