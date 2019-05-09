using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class TemplatesGeneratorTestsWithSwagger
    {
        private TemplatesGenerator _templatesGenerator;
        private string _sourceTemplate;
        private IList<GeneratedTemplate> _generatedTemplates;

        [TestInitialize()]
        public void Initialize()
        {
            _templatesGenerator = new TemplatesGenerator();
            _sourceTemplate = Utils.GetEmbededFileContent("APIManagementTemplate.Test.SamplesTemplate.templateSwagger.json");
            _generatedTemplates = _templatesGenerator.Generate(_sourceTemplate, true, true, separateSwaggerFile:true);
        }

        [TestMethod]
        public void TestResultContainsAPIFor_echoAPI_without_contentFormat_and_contentValue()
        {
            var echoApiTemplate = _generatedTemplates.With(Filename.Echo);
            Assert.IsNotNull(echoApiTemplate);
            var echoApi = echoApiTemplate.WithDirectResource(ResourceType.Api);
            Assert.IsNotNull(echoApi);
            Assert.AreEqual(null , echoApi.Index(Arm.Properties).Value(Arm.ContentFormat));
            Assert.AreEqual(null , echoApi.Index(Arm.Properties).Value(Arm.ContentValue));
        }

        [TestMethod]
        public void TestResultContainsSwaggerAPIFor_HttpBinV2_with_policy_in_resources()
        {
            var apiTemplate = _generatedTemplates.With(Filename.HttpBinV2SwaggerTemplate);
            Assert.IsNotNull(apiTemplate);
            var policies = apiTemplate.WithResources(ResourceType.ApiPolicy);
            Assert.AreEqual(1, policies.Count());

            var policy = policies.First();
            var properties = policy.Index(Arm.Properties);
            Assert.AreEqual("xml-link", properties.Value(Arm.ContentFormat));
            Assert.AreEqual("[concat(parameters('repoBaseUrl'), '/api-Versioned-HTTP-bin-API/v2/api-Versioned-HTTP-bin-API.v2.policy.xml', parameters('_artifactsLocationSasToken'))]", 
                properties.Value(Arm.PolicyContent));
        }

        [TestMethod]
        public void TestResultContainsSwaggerAPIFor_HttpBinV2_with_operation_policy_in_resources()
        {
            var apiTemplate = _generatedTemplates.With(Filename.HttpBinV2SwaggerTemplate);
            Assert.IsNotNull(apiTemplate);
            var policies = apiTemplate.WithResources(ResourceType.OperationPolicy);
            Assert.AreEqual(1, policies.Count());
        }

        [TestMethod]
        public void TestResultContainsOperationPolicyFileFor_EchoAPI_CreateResource()
        {
            var apiTemplate = _generatedTemplates.With(Filename.EchoCreateResourcePolicy);
            Assert.IsNotNull(apiTemplate);
        }

        [TestMethod]
        public void TestResultContainsAPIFor_HttpBinV2_with_no_policy_in_resources()
        {
            var apiTemplate = _generatedTemplates.With(Filename.HttpBinV2);
            Assert.IsNotNull(apiTemplate);
            var policies = apiTemplate.WithResources(ResourceType.ApiPolicy);
            Assert.AreEqual(0, policies.Count());
        }

        [TestMethod]
        public void TestResultContainsAPIFor_HttpBinV2_with_no_operation_policy_in_resources()
        {
            var apiTemplate = _generatedTemplates.With(Filename.HttpBinV2);
            Assert.IsNotNull(apiTemplate);
            var policies = apiTemplate.WithResources(ResourceType.OperationPolicy);
            Assert.AreEqual(0, policies.Count());
        }

        [TestMethod]
        public void TestResultContainsAPIFor_HttpBinV2_with_product_api()
        {
            var apiTemplate = _generatedTemplates.With(Filename.HttpBinV2);
            Assert.IsNotNull(apiTemplate);
            var policies = apiTemplate.WithResources(ResourceType.ProductApi);
            Assert.AreEqual(1, policies.Count());
        }

        [TestMethod]
        public void TestResultContainsProductFor_Starter_with_no_product_api()
        {
            var apiTemplate = _generatedTemplates.With(Filename.ProductStarter);
            Assert.IsNotNull(apiTemplate);
            var productApis = apiTemplate.WithResources(ResourceType.ProductApi);
            Assert.AreEqual(0, productApis.Count());
        }

        [TestMethod]
        public void TestResultContainsSwaggerAPIFor_HttpBinV2_with_parameters()
        {
            var apiTemplate = _generatedTemplates.With(Filename.HttpBinV2SwaggerTemplate);
            Assert.IsNotNull(apiTemplate);
            var parameters = apiTemplate.Content.Index(Arm.Parameters);
            Assert.IsTrue(parameters.Cast<JProperty>().Any(x => x.Name == "service_PreDemoTest_name"));
            Assert.IsTrue(parameters.Cast<JProperty>().Any(x => x.Name == "repoBaseUrl"));
            Assert.IsTrue(parameters.Cast<JProperty>().Any(x => x.Name == "_artifactsLocationSasToken"));
            Assert.AreEqual(3, parameters.Count());
        }

        [TestMethod]
        public void TestResultContainsSwaggerFileFor_echoAPI_with_correct_host()
        {
            var echoApiTemplate = _generatedTemplates.With(Filename.EchoSwagger);
            Assert.IsNotNull(echoApiTemplate);
            Assert.AreEqual("2.0", echoApiTemplate.Content.Value<string>("swagger"));
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateWithoutSwaggerFile()
        {
            var api = _generatedTemplates.With(Filename.HttpBinMaster);
            Assert.IsNotNull(api);
            Assert.IsNull(api.WithDirectResource(Filename.HttpBinV1Swagger, Property.Name));
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateWhereProductStarter()
        {
            var api = _generatedTemplates.With(Filename.MasterTemplate);
            Assert.IsNotNull(api);
            var productStarter = api.WithDirectResource(Filename.ProductStarter, Property.Name);
            Assert.IsNotNull(productStarter);
            var dependsOn = productStarter.Index(Arm.DependsOn);
            Assert.AreEqual(2, dependsOn.Count());
            Assert.IsTrue(dependsOn.Any(x => x.Value<string>().Contains(Filename.Service)));
            Assert.IsTrue(dependsOn.Any(x => x.Value<string>().Contains(Filename.Groups)));
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateWhereSwaggerTemplateDependsOnTemplate()
        {
            var api = _generatedTemplates.With(Filename.HttpBinMaster);
            Assert.IsNotNull(api);
            var swaggerTemplate = api.WithDirectResource(Filename.HttpBinV2SwaggerTemplate, Property.Name);
            Assert.IsNotNull(swaggerTemplate);
            var dependsOn = (JArray) swaggerTemplate.Index(Arm.DependsOn);
            Assert.AreEqual(2, dependsOn.Count);
            Assert.IsTrue(dependsOn.Any(x =>
                x.Value<string>().Contains(Filename.HttpBinV2)));
        }

        [TestMethod]
        public void GetOperationName_Normal()
        {
            TestGetOperationName("Purchase", "POST", "Purchase");
        }

        [TestMethod]
        public void GetOperationName_ApiAndMethodLowercase()
        {
            TestGetOperationName("api-Purchase-post", "POST", "Purchase");
        }

        [TestMethod]
        public void GetOperationName_ApiAndMethodUppercase()
        {
            TestGetOperationName("api-Purchase-POST", "POST", "Purchase");
        }

        [TestMethod]
        public void GetOperationName_Api()
        {
            TestGetOperationName("api-Purchase", "POST", "api-Purchase");
        }

        private static void TestGetOperationName(string name, string method, string expected)
        {
            var result = TemplateGenerator.GetOperationName(JObject.FromObject(new {name = name, properties = new {method = method}}));
            Assert.AreEqual(expected, result);
        }
    }

}