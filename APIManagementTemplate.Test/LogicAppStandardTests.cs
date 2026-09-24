using System.Collections.Generic;
using System.Linq;
using APIManagementTemplate.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using BackendProperty = APIManagementTemplate.Models.Property;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class LogicAppStandardTests
    {
        private const string SubscriptionId = "c107df29-a4af-4bc9-a733-f88f0eaa4296";
        private const string ResourceGroup = "contoso-rg";
        private const string ServiceName = "contosoapim";

        private const string CallbackUrlPrefix =
            "listCallbackUrl(resourceId(parameters('LogicAppStandard_contosola_subscriptionId'),parameters('LogicAppStandard_contosola_resourceGroup'), 'Microsoft.Web/sites/hostruntime/webhooks/api/workflows/triggers', parameters('LogicAppStandard_contosola_siteName'), 'runtime', 'workflow', 'management'";

        private JObject GenerateTemplate(bool replaceLogicAppStandardSignatureWithNamedValue)
        {
            var generator = new TemplateGenerator(ServiceName, SubscriptionId, ResourceGroup, "planner",
                false, false, false, false, new MockResourceCollector("LogicAppStandard"),
                replaceLogicAppStandardSignatureWithNamedValue: replaceLogicAppStandardSignatureWithNamedValue);
            return generator.GenerateTemplate().GetAwaiter().GetResult();
        }

        private static IEnumerable<JObject> AllResources(JObject template)
        {
            return Flatten(template.Value<JArray>("resources"));
        }

        private static IEnumerable<JObject> Flatten(JArray resources)
        {
            foreach (var resource in (resources ?? new JArray()).OfType<JObject>())
            {
                yield return resource;
                foreach (var child in Flatten(resource.Value<JArray>("resources")))
                {
                    yield return child;
                }
            }
        }

        private static JObject GetNamedValue(JObject template, string name)
        {
            return AllResources(template).FirstOrDefault(r =>
                r.Value<string>("type") == "Microsoft.ApiManagement/service/namedValues" &&
                r.Value<string>("name").Contains($"'{name}'"));
        }

        private static string GetOperationPolicyContent(JObject template, string operationName)
        {
            var policy = AllResources(template).First(r =>
                r.Value<string>("type") == "Microsoft.ApiManagement/service/apis/operations/policies" &&
                r.Value<string>("name").Contains($"'{operationName}'"));
            return policy["properties"].Value<string>("policyContent");
        }

        [TestMethod]
        public void BackendFromApiPolicyIsExported()
        {
            var template = GenerateTemplate(false);

            var backend = AllResources(template).FirstOrDefault(r =>
                r.Value<string>("type") == "Microsoft.ApiManagement/service/backends");

            Assert.IsNotNull(backend, "The backend that is only referenced from the API policy should be exported.");
            Assert.IsTrue(backend.Value<string>("name").Contains("LogicAppStandard_contosola"));
        }

        [TestMethod]
        public void ExistingNamedValueSignatureIsResolvedWithListCallbackUrl()
        {
            var template = GenerateTemplate(false);

            var namedValue = GetNamedValue(template, "planner_createorder_sig");

            Assert.IsNotNull(namedValue);
            Assert.AreEqual(
                $"[{CallbackUrlPrefix}, 'CreateOrder', 'When_a_HTTP_request_is_received'), '2022-03-01').queries.sig]",
                namedValue["properties"].Value<string>("value"));
        }

        [TestMethod]
        public void LiteralSignatureIsKeptWhenReplacementIsDisabled()
        {
            var template = GenerateTemplate(false);

            Assert.IsTrue(GetOperationPolicyContent(template, "cancelorder")
                .Contains("sig=Xo9KpzDqQJ1nO2Ic6rT3lVuB4aWmE5HsN7Yg0FdCbUk"));
            Assert.IsNull(GetNamedValue(template, "CancelOrder-sig"));
        }

        [TestMethod]
        public void LiteralSignatureIsReplacedByNamedValueWhenEnabled()
        {
            var template = GenerateTemplate(true);

            Assert.IsTrue(GetOperationPolicyContent(template, "cancelorder").Contains("sig={{CancelOrder-sig}}"));
        }

        [TestMethod]
        public void NamedValueForLiteralSignatureIsResolvedWithListCallbackUrl()
        {
            var template = GenerateTemplate(true);

            var namedValue = GetNamedValue(template, "CancelOrder-sig");

            Assert.IsNotNull(namedValue);
            Assert.AreEqual(
                $"[{CallbackUrlPrefix}, 'CancelOrder', 'When_a_HTTP_request_is_received'), '2022-03-01').queries.sig]",
                namedValue["properties"].Value<string>("value"));
        }

        [TestMethod]
        public void ApiDependsOnNamedValueForLiteralSignature()
        {
            var template = GenerateTemplate(true);

            var api = AllResources(template).First(r => r.Value<string>("type") == "Microsoft.ApiManagement/service/apis");

            Assert.IsTrue(api.Value<JArray>("dependsOn").Values<string>().Any(d =>
                d.Contains("'Microsoft.ApiManagement/service/namedValues'") && d.Contains("'CancelOrder-sig'")));
        }

        [TestMethod]
        public void BackendIsRecognizedAsLogicAppStandard()
        {
            var template = new DeploymentTemplate();
            var backend = JObject.Parse(
                Utils.GetEmbededFileContent(
                    "APIManagementTemplate.Test.Samples.LogicAppStandard.service-contosoapim-backends-LogicAppStandard_contosola.json"));
            var site = JObject.Parse(
                Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.LogicAppStandard.sites-contosola.json"));

            var property = template.AddBackend(backend, site, null);

            Assert.IsNotNull(property);
            Assert.AreEqual(BackendProperty.PropertyType.LogicAppStandard, property.type);
            Assert.AreEqual("contosola", property.name);
        }

        [TestMethod]
        public void WebAppBackendWithoutWorkflowKindIsNotLogicAppStandard()
        {
            var template = new DeploymentTemplate();
            var backend = JObject.Parse(
                Utils.GetEmbededFileContent(
                    "APIManagementTemplate.Test.Samples.LogicAppStandard.service-contosoapim-backends-LogicAppStandard_contosola.json"));
            var site = JObject.Parse(
                Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.LogicAppStandard.sites-contosola.json"));
            site["kind"] = "functionapp";

            var property = template.AddBackend(backend, site, null);

            Assert.IsNotNull(property);
            Assert.AreEqual(BackendProperty.PropertyType.Function, property.type);
        }
    }
}
