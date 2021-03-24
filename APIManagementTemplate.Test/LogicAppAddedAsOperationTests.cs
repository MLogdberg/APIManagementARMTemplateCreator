using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class LogicAppAddedAsOperationTests
    {
        private IResourceCollector collector;
        [TestInitialize()]
        public void Initialize()
        {
            this.collector = new MockResourceCollector("LogicAppAddedAsOperation");

        }

        private TemplateGenerator GetTemplateGenerator()
        {
            return new TemplateGenerator("iBizUtbildningAPIM", "a4525dcb-b7ac-4677-b104-97bde6a8f41d", "LabResources", "path eq 'sales/orders'", false, true, false, false, this.collector,exportTags:true);
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
            Assert.AreEqual("iBizUtbildningAPIM", obj["service_iBizUtbildningAPIM_name"].Value<string>("defaultValue"));
            Assert.AreEqual("sales-orders", obj["api_sales-orders_name"].Value<string>("defaultValue"));
            Assert.AreEqual("1", obj["sales-orders_apiRevision"].Value<string>("defaultValue"));
            Assert.AreEqual("http://localhost", obj["sales-orders_serviceUrl"].Value<string>("defaultValue"));
            Assert.AreEqual(true, obj["sales-orders_isCurrent"].Value<bool>("defaultValue"));
            Assert.AreEqual("LabResources", obj["LogicApp_salesinvoice-la_LabResources_resourceGroup"].Value<string>("defaultValue"));
            Assert.AreEqual("salesinvoice-la", obj["LogicApp_salesinvoice-la_LabResources_logicAppName"].Value<string>("defaultValue"));
            Assert.AreEqual("sales-api-s", obj["product_sales-api-s_name"].Value<string>("defaultValue"));
        }
        [TestMethod]
        public void TestResourcesCount()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = (JArray)template["resources"];
            Assert.AreEqual(4, obj.Count);
        }

        [TestMethod]
        public void TestResourcesBackend()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/backends").First();

            Assert.AreEqual("Microsoft.ApiManagement/service/backends", obj.Value<string>("type"));
            Assert.AreEqual("2019-01-01", obj.Value<string>("apiVersion"));
            
            Assert.AreEqual("[concat(parameters('service_iBizUtbildningAPIM_name'), '/' ,'LogicApp_salesinvoice-la_LabResources')]", obj.Value<string>("name"));
            Assert.AreEqual(0, obj["resources"].Count());
            Assert.AreEqual(0, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("[substring(listCallbackUrl(resourceId(parameters('LogicApp_salesinvoice-la_LabResources_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_salesinvoice-la_LabResources_logicAppName'), 'manual'), '2017-07-01').basePath,0,add(10,indexOf(listCallbackUrl(resourceId(parameters('LogicApp_salesinvoice-la_LabResources_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_salesinvoice-la_LabResources_logicAppName'), 'manual'), '2017-07-01').basePath,'/triggers/')))]", prop.Value<string>("url"));
            Assert.AreEqual("http", prop.Value<string>("protocol"));
            Assert.AreEqual("[concat('https://management.azure.com/','subscriptions/',subscription().subscriptionId,'/resourceGroups/',parameters('LogicApp_salesinvoice-la_LabResources_resourceGroup'),'/providers/Microsoft.Logic/workflows/',parameters('LogicApp_salesinvoice-la_LabResources_logicAppName'))]", prop.Value<string>("resourceId"));
        }

        [TestMethod]
        public void TestResourcesProperties()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/namedValues").First();

            Assert.AreEqual("Microsoft.ApiManagement/service/namedValues", obj.Value<string>("type"));
            Assert.AreEqual("2020-06-01-preview", obj.Value<string>("apiVersion"));

            Assert.AreEqual("[concat(parameters('service_iBizUtbildningAPIM_name'), '/', '5cc9f8a843285d9c7cdc3e3d')]", obj.Value<string>("name"));
            Assert.AreEqual(0, obj["resources"].Count());
            Assert.AreEqual(0, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("[listCallbackUrl(resourceId(parameters('LogicApp_salesinvoice-la_LabResources_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_salesinvoice-la_LabResources_logicAppName'), 'manual'), '2017-07-01').queries.sig]", prop.Value<string>("value"));
            Assert.AreEqual(true, prop.Value<bool>("secret"));
            Assert.AreEqual(0, prop["tags"].Count());
        }


        [TestCleanup()]
        public void Cleanup()
        {

        }
    }
}
