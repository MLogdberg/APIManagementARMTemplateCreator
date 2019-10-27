using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class LogicAppManualTests
    {
        private IResourceCollector collector;
        [TestInitialize()]
        public void Initialize()
        {            
            this.collector = new MockResourceCollector("LogicAppManual");
            
        }

        private TemplateGenerator GetTemplateGenerator()
        {
            return new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest", "maloapimtestmanual", false, false, false, false, this.collector);
        }



        [TestMethod]
        public void LoadLogicAppManual()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            Assert.IsNotNull(template);
            
        }
        
        [TestMethod]
        public void TestParameters()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = template["parameters"];
            Assert.AreEqual("ibizmalo", obj["service_ibizmalo_name"].Value<string>("defaultValue"));
            Assert.AreEqual("malologicapptestmanual", obj["api_malologicapptestmanual_name"].Value<string>("defaultValue"));
            Assert.AreEqual("1", obj["malologicapptestmanual_apiRevision"].Value<string>("defaultValue"));
            Assert.AreEqual("https://prod-27.westeurope.logic.azure.com/workflows/a8beb87660a44d1f90d427be186160c7/triggers", obj["malologicapptestmanual_serviceUrl"].Value<string>("defaultValue"));
            Assert.AreEqual(true, obj["malologicapptestmanual_isCurrent"].Value<bool>("defaultValue"));
            Assert.AreEqual("maloapimtest", obj["LogicApp_malologicapptestManual_resourceGroup"].Value<string>("defaultValue"));
            Assert.AreEqual("malologicapptestManual", obj["LogicApp_malologicapptestManual_logicAppName"].Value<string>("defaultValue"));
        }
        [TestMethod]
        public void TestResourcesCount()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = (JArray)template["resources"];
            Assert.AreEqual(3, obj.Count);
        }

        [TestMethod]
        public void TestResourcesBackend()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = ((JArray)template["resources"]).Where( rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/backends").First();

            Assert.AreEqual("Microsoft.ApiManagement/service/backends", obj.Value<string>("type"));
            Assert.AreEqual("2019-09-01", obj.Value<string>("apiVersion"));

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/' ,'LogicApp_malologicapptestManual')]", obj.Value<string>("name"));
            Assert.AreEqual(0, obj["resources"].Count());
            Assert.AreEqual(0, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("[substring(listCallbackUrl(resourceId(parameters('LogicApp_malologicapptestManual_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_malologicapptestManual_logicAppName'), 'manual'), '2017-07-01').basePath,0,add(10,indexOf(listCallbackUrl(resourceId(parameters('LogicApp_malologicapptestManual_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_malologicapptestManual_logicAppName'), 'manual'), '2017-07-01').basePath,'/triggers/')))]", prop.Value<string>("url"));
            Assert.AreEqual("http", prop.Value<string>("protocol"));
            Assert.AreEqual("[concat('https://management.azure.com/','subscriptions/',subscription().subscriptionId,'/resourceGroups/',parameters('LogicApp_malologicapptestManual_resourceGroup'),'/providers/Microsoft.Logic/workflows/',parameters('LogicApp_malologicapptestManual_logicAppName'))]", prop.Value<string>("resourceId"));
        }

        [TestMethod]
        public void TestResourcesProperties()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/properties").First();

            Assert.AreEqual("Microsoft.ApiManagement/service/properties", obj.Value<string>("type"));
            Assert.AreEqual("2019-09-01", obj.Value<string>("apiVersion"));

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', '5b418f9eb3de406c8744638f')]", obj.Value<string>("name"));
            Assert.AreEqual(0, obj["resources"].Count());
            Assert.AreEqual(0, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("[listCallbackUrl(resourceId(parameters('LogicApp_malologicapptestManual_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_malologicapptestManual_logicAppName'), 'manual'), '2017-07-01').queries.sig]", prop.Value<string>("value"));
            Assert.AreEqual(true, prop.Value<bool>("secret"));
            Assert.AreEqual(0, prop["tags"].Count());
        }


        [TestCleanup()]
        public void Cleanup()
        {

        }
    }
}
