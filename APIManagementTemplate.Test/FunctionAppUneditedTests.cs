using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class FunctionAppUneditedTests
    {
        private IResourceCollector collector;
        [TestInitialize()]
        public void Initialize()
        {
            this.collector = new MockResourceCollector("FunctionAppUnedited");

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
        public void GenerateTemplate()
        {
            var template = GetTemplate();
            
            Assert.IsNotNull(template);

        }

        [TestMethod]
        public void TestParameters()
        {
            var template = GetTemplate();

            var obj = template["parameters"];
            Assert.AreEqual("ibizmalo", obj["service_ibizmalo_name"].Value<string>("defaultValue"));
            Assert.AreEqual("maloapimtestclean", obj["api_maloapimtestclean_name"].Value<string>("defaultValue"));
            Assert.AreEqual("1", obj["maloapimtestclean_apiRevision"].Value<string>("defaultValue"));
            Assert.AreEqual("https://maloapimtest.azurewebsites.net/", obj["maloapimtestclean_serviceUrl"].Value<string>("defaultValue"));
            Assert.AreEqual("v1", obj["maloapimtestclean_apiVersion"].Value<string>("defaultValue"));
            Assert.AreEqual(true, obj["maloapimtestclean_isCurrent"].Value<bool>("defaultValue"));
            Assert.AreEqual("maloapimtest", obj["FunctionApp_maloapimtest_resourceGroup"].Value<string>("defaultValue"));
            Assert.AreEqual("maloapimtest", obj["FunctionApp_maloapimtest_siteName"].Value<string>("defaultValue"));
        }

        [TestMethod]
        public void TestResourcesCount()
        {
            var template = GetTemplate();

            var obj = (JArray)template["resources"];
            Assert.AreEqual(6, obj.Count);
        }
        
        [TestMethod]
        public void TestResourcesBackend()
        {
            var template = GetTemplate();

            var obj = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/backends").First();

            Assert.AreEqual("Microsoft.ApiManagement/service/backends", obj.Value<string>("type"));
            Assert.AreEqual("2019-09-01", obj.Value<string>("apiVersion"));
            
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/' ,'FunctionApp_maloapimtest')]", obj.Value<string>("name"));
            Assert.AreEqual(0, obj["resources"].Count());
            Assert.AreEqual(0, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("[concat('https://',toLower(parameters('FunctionApp_maloapimtest_siteName')),'.azurewebsites.net/')]", prop.Value<string>("url"));
            Assert.AreEqual("http", prop.Value<string>("protocol"));
            Assert.AreEqual("[concat('https://management.azure.com/','subscriptions/',subscription().subscriptionId,'/resourceGroups/',parameters('FunctionApp_maloapimtest_resourceGroup'),'/providers/Microsoft.Web/sites/',parameters('FunctionApp_maloapimtest_siteName'))]", prop.Value<string>("resourceId"));
        }

        [TestMethod]
        public void TestResourcesProperties()
        {
            var template = GetTemplate();
            var properties = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/properties");
            Assert.AreEqual(3, properties.Count());
            foreach (var obj in properties)
            {
                Assert.AreEqual("Microsoft.ApiManagement/service/properties", obj.Value<string>("type"));
                Assert.AreEqual("2019-09-01", obj.Value<string>("apiVersion"));

                Assert.AreEqual(0, obj["resources"].Count());
                Assert.AreEqual(0, obj["dependsOn"].Count());

                var prop = obj["properties"];
                if(prop.Value<string>("displayName") == "maloapimtest_GenericWebhook_query_5b41934ca550d9de49391585")
                {
                    Assert.AreEqual("[listsecrets(resourceId(parameters('FunctionApp_maloapimtest_resourceGroup'),'Microsoft.Web/sites/functions', parameters('FunctionApp_maloapimtest_siteName'), 'GenericWebhook'),'2015-08-01').key]", prop.Value<string>("value"));
                }else if (prop.Value<string>("displayName") == "maloapimtest_HTTPTrigger_query_5b41934c571f50d55fdbf71b")
                {
                    Assert.AreEqual("[listsecrets(resourceId(parameters('FunctionApp_maloapimtest_resourceGroup'),'Microsoft.Web/sites/functions', parameters('FunctionApp_maloapimtest_siteName'), 'HTTPTrigger'),'2015-08-01').key]", prop.Value<string>("value"));
                }
                else if (prop.Value<string>("displayName") == "maloapimtest_HttpTriggerAdminKey_query_5b41934c6d0f59440d20c5ee")
                {
                    //hwo to fix the admin key?????
                    Assert.AreEqual("[listsecrets(resourceId(parameters('FunctionApp_maloapimtest_resourceGroup'),'Microsoft.Web/sites/functions', parameters('FunctionApp_maloapimtest_siteName'), 'HttpTriggerAdminKey'),'2015-08-01').key]", prop.Value<string>("value"));
                }
                else
                {
                    Assert.Fail("Extra properties!");
                }

                Assert.AreEqual(true, prop.Value<bool>("secret"));
                Assert.AreEqual(0, prop["tags"].Count());
            }
        }

        [TestMethod]
        public void TestApiVersionSetIdForProductApiIsSetWithResourceId()
        {
            var template = GetTemplate(true);
            var products = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/products");
            var productApis = products.First()["resources"].Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/products/apis");
            var apiVersionSet = productApis.First()["properties"]["apiVersionSetId"];
            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service/apiVersionSets', parameters('service_ibizmalo_name'), '5b419345805ee415de572191')]", apiVersionSet);
        }

        [TestMethod]
        public void TestProductApiNameHasParameterizedApiName()
        {
            var template = GetTemplate(true);
            var products = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/products");
            var productApis = products.First()["resources"].Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/products/apis");
            var productApiName = productApis.First()["name"];
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', parameters('product_57cd81afe568130059060001_name'), '/', parameters('api_maloapimtestclean_name'))]", productApiName);
        }

        [TestCleanup()]
        public void Cleanup()
        {

        }
    }
}
