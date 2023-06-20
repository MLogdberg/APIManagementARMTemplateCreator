using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class FunctionAppMixedWithLogicAppTests
    {
        private IResourceCollector collector;
        [TestInitialize()]
        public void Initialize()
        {
            this.collector = new MockResourceCollector("FunctionAppMixedWithLogicApp");

        }
        private JObject _template = null;
        private JObject GetTemplate()
        {
            if (this._template != null)
                return this._template;
            var generator = new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest", "maloapimtest", false, false, false, false, this.collector);
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
            Assert.AreEqual("maloapimtest", obj["api_maloapimtest_name"].Value<string>("defaultValue"));
            Assert.AreEqual("1", obj["maloapimtest_apiRevision"].Value<string>("defaultValue"));
            Assert.AreEqual("https://maloapimtest.azurewebsites.net/", obj["maloapimtest_serviceUrl"].Value<string>("defaultValue"));
            Assert.AreEqual("v1", obj["maloapimtest_apiVersion"].Value<string>("defaultValue"));
            Assert.AreEqual(true, obj["maloapimtest_isCurrent"].Value<bool>("defaultValue"));
            //Function App
            Assert.AreEqual("maloapimtest", obj["FunctionApp_maloapimtest_resourceGroup"].Value<string>("defaultValue"));
            Assert.AreEqual("maloapimtest", obj["FunctionApp_maloapimtest_siteName"].Value<string>("defaultValue"));
            //Logic App ++
            Assert.AreEqual("maloapimtest", obj["LogicApp_malologicapptestRequest_resourceGroup"].Value<string>("defaultValue"));
            Assert.AreEqual("malologicapptestRequest", obj["LogicApp_malologicapptestRequest_logicAppName"].Value<string>("defaultValue"));
        }

        [TestMethod]
        public void TestResourcesCount()
        {
            var template = GetTemplate();

            var obj = (JArray)template["resources"];
            Assert.AreEqual(9, obj.Count);
        }

        [TestMethod]
        public void TestResourcesBackend()
        {
            var template = GetTemplate();

            foreach (var obj in ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/backends"))
            {

                Assert.AreEqual("Microsoft.ApiManagement/service/backends", obj.Value<string>("type"));
                Assert.AreEqual("2019-01-01", obj.Value<string>("apiVersion"));

                if (obj.Value<string>("name") == "[concat(parameters('service_ibizmalo_name'), '/' ,'FunctionApp_maloapimtest')]")
                {
                    Assert.AreEqual(0, obj["resources"].Count());
                    Assert.AreEqual(0, obj["dependsOn"].Count());

                    var prop = obj["properties"];
                    Assert.AreEqual("[concat('https://',first(reference(resourceId(parameters('FunctionApp_maloapimtest_subscriptionId'),parameters('FunctionApp_maloapimtest_resourceGroup'),concat('Microsoft.Web/sites'),parameters('FunctionApp_maloapimtest_siteName')),'2022-03-01').hostNames))]", prop.Value<string>("url"));
                    Assert.AreEqual("http", prop.Value<string>("protocol"));
                    Assert.AreEqual("[concat('https://management.azure.com/','subscriptions/',parameters('FunctionApp_maloapimtest_subscriptionId'),'/resourceGroups/',parameters('FunctionApp_maloapimtest_resourceGroup'),'/providers/Microsoft.Web/sites/',parameters('FunctionApp_maloapimtest_siteName'))]", prop.Value<string>("resourceId"));
                }
                else
                 if (obj.Value<string>("name") == "[concat(parameters('service_ibizmalo_name'), '/' ,'LogicApp_malologicapptestRequest')]")
                {
                    Assert.AreEqual(0, obj["resources"].Count());
                    Assert.AreEqual(0, obj["dependsOn"].Count());

                    var prop = obj["properties"];
                    Assert.AreEqual("[substring(listCallbackUrl(resourceId(parameters('LogicApp_malologicapptestRequest_subscriptionId'),parameters('LogicApp_malologicapptestRequest_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_malologicapptestRequest_logicAppName'), 'request'), '2017-07-01').basePath,0,add(10,indexOf(listCallbackUrl(resourceId(parameters('LogicApp_malologicapptestRequest_subscriptionId'),parameters('LogicApp_malologicapptestRequest_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_malologicapptestRequest_logicAppName'), 'request'), '2017-07-01').basePath,'/triggers/')))]", prop.Value<string>("url"));
                    Assert.AreEqual("http", prop.Value<string>("protocol"));
                    Assert.AreEqual("[concat('https://management.azure.com/','subscriptions/',parameters('LogicApp_malologicapptestRequest_subscriptionId'),'/resourceGroups/',parameters('LogicApp_malologicapptestRequest_resourceGroup'),'/providers/Microsoft.Logic/workflows/',parameters('LogicApp_malologicapptestRequest_logicAppName'))]", prop.Value<string>("resourceId"));
                }
                else
                {
                    Assert.Fail("Extra backends!");
                }
            }
        }

        [TestMethod]
        public void TestResourcesProperties()
        {
            var template = GetTemplate();
            foreach (var obj in ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/namedValues"))
            {
                Assert.AreEqual("Microsoft.ApiManagement/service/namedValues", obj.Value<string>("type"));
                Assert.AreEqual("2020-06-01-preview", obj.Value<string>("apiVersion"));

                Assert.AreEqual(0, obj["resources"].Count());
                Assert.AreEqual(0, obj["dependsOn"].Count());

                var prop = obj["properties"];
                if (prop.Value<string>("displayName") == "maloapimtest_GenericWebhook_query_5b418f4619afb685dc8de379")
                {
                    Assert.AreEqual("[listKeys(resourceId(parameters('FunctionApp_maloapimtest_subscriptionId'),parameters('FunctionApp_maloapimtest_resourceGroup'),concat('Microsoft.Web/sites/host'),parameters('FunctionApp_maloapimtest_siteName'),'default'),'2018-02-01').functionKeys.default]", prop.Value<string>("value"));
                }
                else if (prop.Value<string>("displayName") == "maloapimtest_HTTPTrigger_query_5b418f463f37b79bfde7eebe")
                {
                    Assert.AreEqual("[listKeys(resourceId(parameters('FunctionApp_maloapimtest_subscriptionId'),parameters('FunctionApp_maloapimtest_resourceGroup'),concat('Microsoft.Web/sites/host'),parameters('FunctionApp_maloapimtest_siteName'),'default'),'2018-02-01').functionKeys.default]", prop.Value<string>("value"));
                }
                else if (prop.Value<string>("displayName") == "maloapimtest_HttpTriggerAdminKey_query_5b418f46b3882daea0919d26")
                {
                    //hwo to fix the admin key?????
                    Assert.AreEqual("[listKeys(resourceId(parameters('FunctionApp_maloapimtest_subscriptionId'),parameters('FunctionApp_maloapimtest_resourceGroup'),concat('Microsoft.Web/sites/host'),parameters('FunctionApp_maloapimtest_siteName'),'default'),'2018-02-01').functionKeys.default]", prop.Value<string>("value"));
                }
                else if (prop.Value<string>("displayName") == "maloapimtest_request-invoke_5b4192e76a19ef3c6dbf2466")
                {
                    Assert.AreEqual("[listCallbackUrl(resourceId(parameters('LogicApp_malologicapptestRequest_subscriptionId'),parameters('LogicApp_malologicapptestRequest_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_malologicapptestRequest_logicAppName'), 'request'), '2017-07-01').queries.sig]", prop.Value<string>("value"));
                }
                else if (prop.Value<string>("displayName") == "maloapimtest_request-invoke-1_5b419313d3e5a75808e3f4ce")
                {
                    Assert.AreEqual("[listCallbackUrl(resourceId(parameters('LogicApp_malologicapptestRequest_subscriptionId'),parameters('LogicApp_malologicapptestRequest_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_malologicapptestRequest_logicAppName'), 'request'), '2017-07-01').queries.sig]", prop.Value<string>("value"));
                }
                else
                {
                    Assert.Fail("Extra properties!");
                }

                Assert.AreEqual(true, prop.Value<bool>("secret"));
                Assert.AreEqual(0, prop["tags"].Count());
            }
        }


        [TestCleanup()]
        public void Cleanup()
        {

        }
    }
}
