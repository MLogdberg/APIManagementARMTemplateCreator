using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class LogicAppCustomTriggerNameTest
    {
        private IResourceCollector collector;
        [TestInitialize()]
        public void Initialize()
        {
            this.collector = new MockResourceCollector("LogicAppCustomTriggerName");

        }

        private TemplateGenerator GetTemplateGenerator()
        {
            return new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest", "customtrigger", false, false, false, false, this.collector);
        }



        [TestMethod]
        public void LoadLogicAppCustom()
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
            Assert.AreEqual("custom-trigger-displayname", obj["api_custom-trigger-displayname_name"].Value<string>("defaultValue"));
            Assert.AreEqual("1", obj["custom-trigger-displayname_apiRevision"].Value<string>("defaultValue"));
            Assert.AreEqual("https://prod-26.westeurope.logic.azure.com/workflows/18edd660cbae4a8da712a2f3fc1a1122/triggers", obj["custom-trigger-displayname_serviceUrl"].Value<string>("defaultValue"));            
            Assert.AreEqual(true, obj["custom-trigger-displayname_isCurrent"].Value<bool>("defaultValue"));
            Assert.AreEqual("maloapimtest", obj["LogicApp_customtrigger_resourceGroup"].Value<string>("defaultValue"));
            Assert.AreEqual("customtrigger", obj["LogicApp_customtrigger_logicAppName"].Value<string>("defaultValue"));
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
            var obj = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/backends").First();

            Assert.AreEqual("Microsoft.ApiManagement/service/backends", obj.Value<string>("type"));
            Assert.AreEqual("2019-01-01", obj.Value<string>("apiVersion"));
            
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/' ,'LogicApp_customtrigger')]", obj.Value<string>("name"));
            Assert.AreEqual(0, obj["resources"].Count());
            Assert.AreEqual(0, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("[substring(listCallbackUrl(resourceId(parameters('LogicApp_customtrigger_subscriptionId'),parameters('LogicApp_customtrigger_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_customtrigger_logicAppName'), 'customtriggername'), '2017-07-01').basePath,0,add(10,indexOf(listCallbackUrl(resourceId(parameters('LogicApp_customtrigger_subscriptionId'),parameters('LogicApp_customtrigger_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_customtrigger_logicAppName'), 'customtriggername'), '2017-07-01').basePath,'/triggers/')))]", prop.Value<string>("url"));
            Assert.AreEqual("http", prop.Value<string>("protocol"));
            Assert.AreEqual("[concat('https://management.azure.com/','subscriptions/',parameters('LogicApp_customtrigger_subscriptionId'),'/resourceGroups/',parameters('LogicApp_customtrigger_resourceGroup'),'/providers/Microsoft.Logic/workflows/',parameters('LogicApp_customtrigger_logicAppName'))]", prop.Value<string>("resourceId"));
        }

        [TestMethod]
        public void TestResourcesProperties()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/namedValues").First();

            Assert.AreEqual("Microsoft.ApiManagement/service/namedValues", obj.Value<string>("type"));
            Assert.AreEqual("2020-06-01-preview", obj.Value<string>("apiVersion"));

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', '5b699d8fcc9520e91fe87288')]", obj.Value<string>("name"));
            Assert.AreEqual(0, obj["resources"].Count());
            Assert.AreEqual(0, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("[listCallbackUrl(resourceId(parameters('LogicApp_customtrigger_subscriptionId'),parameters('LogicApp_customtrigger_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_customtrigger_logicAppName'), 'customtriggername'), '2017-07-01').queries.sig]", prop.Value<string>("value"));
            Assert.AreEqual(true, prop.Value<bool>("secret"));
            Assert.AreEqual(0, prop["tags"].Count());
        }


        [TestCleanup()]
        public void Cleanup()
        {

        }
    }
}
