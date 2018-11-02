using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class WithoutApiVersionSetIdTests
    {
        private const string ProductPolicyType = "Microsoft.ApiManagement/service/products/policies";
        private const string ApiType = "Microsoft.ApiManagement/service/apis";
        private const string ProductResourceType = "Microsoft.ApiManagement/service/apis/products";
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
            var generator = new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest", "maloapimtestclean", false, exportProducts, false, true, this.collector);
            this._template = generator.GenerateTemplate().GetAwaiter().GetResult();
            return this._template;
        }


        private JToken GetResourceFromTemplate(string resourceType)
        {
            var template = GetTemplate(true);
            return template["resources"].FirstOrDefault(rr => rr["type"].Value<string>() == resourceType);
        }

        [TestMethod]
        public void TestApiVersionSetIdForApiIsNotSet()
        {
            JToken api = GetResourceFromTemplate(ApiType);
            Assert.IsNotNull(api);

            var versionSetId = api["properties"]["apiVersionSetId"];
            Assert.IsNull(versionSetId);
        }



        [TestMethod]
        public void TestProductContainsPolicy()
        {
            IEnumerable<JToken> policies = GetSubResourceFromTemplate(ProductResourceType, ProductPolicyType);

            Assert.AreEqual(1, policies.Count());
        }

        private IEnumerable<JToken> GetSubResourceFromTemplate(string resourceType, string subResourceType)
        {
            JToken resource = GetResourceFromTemplate(resourceType);
            return resource.Value<JArray>("resources").Where(x => x.Value<string>("type") == subResourceType);
        }

        [TestMethod]
        public void TestProductContainsPolicyWithCorrectName()
        {
            var policy = GetSubResourceFromTemplate(ProductResourceType, ProductPolicyType).First();

            var name = policy.Value<string>("name");
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', 'unlimited', '/policy')]", name);
        }

        [TestMethod]
        public void TestProductContainsPolicyThatDependsOnProduct()
        {
            var policy = GetSubResourceFromTemplate(ProductResourceType, ProductPolicyType).First();

            var dependsOn = policy.Value<JArray>("dependsOn");
            Assert.IsTrue(dependsOn.Any(x =>
                x.Value<string>() ==
                "[resourceId('Microsoft.ApiManagement/service/products', parameters('service_ibizmalo_name'), 'unlimited')]"));
        }

    }
}
