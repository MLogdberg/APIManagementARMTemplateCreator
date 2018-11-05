using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class TemplatesGeneratorTests
    {
        private const string HttpBinV1Filename = "api-Versioned-HTTP-bin-API.v1.template.json";
        private const string HttpBinV2Filename = "api-Versioned-HTTP-bin-API.v2.template.json";
        private const string EchoFilename = "api-Echo-API.template.json";
        private const string JPathAPI = "$.resources[?(@.type=='Microsoft.ApiManagement/service/apis')]";
        private const string JPathParameters = "$.parameters.*";
        private const string HttpBinVersionSetFilename = "api-Versioned-HTTP-bin-API.version-set.template.json";
        private const string ServiceFilename = "service.template.json";
        private const string ProductStarterFilename = "product-starter.template.json";
        private TemplatesGenerator _templatesGenerator;
        private string _sourceTemplate;
        private IList<GeneratedTemplate> _generatedTemplates;

        [TestInitialize()]
        public void Initialize()
        {
            _templatesGenerator = new TemplatesGenerator();
            _sourceTemplate = Utils.GetEmbededFileContent("APIManagementTemplate.Test.SamplesTemplate.template.json");
            _generatedTemplates = _templatesGenerator.Generate(_sourceTemplate, true, true);
        }

        [TestMethod]
        public void TestResultIsNotNull()
        {
            Assert.IsNotNull(_generatedTemplates);
        }

        [TestMethod]
        public void TestResultContainsCorrectNumberOfItems()
        {
            Assert.AreEqual(18, _generatedTemplates.Count);
        }
        [TestMethod]
        public void TestResultContains_httpbinv1()
        {
            Assert.IsTrue(_generatedTemplates.Any(x =>
                x.FileName == HttpBinV1Filename &&
                x.Directory == @"api-Versioned-HTTP-bin-API\v1"));
        }

        [TestMethod]
        public void TestResultContains_httpbinv2()
        {
            Assert.IsTrue(_generatedTemplates.Any(x =>
                x.FileName == HttpBinV2Filename &&
                x.Directory == @"api-Versioned-HTTP-bin-API\v2"));
        }

        [TestMethod]
        public void TestResultContains_ProductStarter()
        {
            Assert.IsTrue(_generatedTemplates.Any(x =>
                x.FileName == ProductStarterFilename &&
                x.Directory == @"product-starter" ));
        }

        [TestMethod]
        public void TestResultContainsPolicyFor_Starter()
        {
            IEnumerable<JToken> policies = GetPoliciesForProduct(ProductStarterFilename);
            Assert.AreEqual(1, policies.Count());
        }

        [TestMethod]
        public void TestResultContainsPolicyFor_EchoApiCreateResource()
        {
            AssertPolicyFile("api-Echo-API.create-resource.policy.xml", "api-Echo-API");
        }

        [TestMethod]
        public void TestResultContainsPolicyFor_HttpBinPutCreateResource()
        {
            AssertPolicyFile("api-Versioned-HTTP-bin-API.v2.put.policy.xml", "api-Versioned-HTTP-bin-API\\v2");
        }

        [TestMethod]
        public void TestResultContainsPolicyFor_HttpBin()
        {
            AssertPolicyFile("api-Versioned-HTTP-bin-API.v2.policy.xml", "api-Versioned-HTTP-bin-API\\v2");
        }

        private void AssertPolicyFile(string policyFile, string directory)
        {
            var policies = _generatedTemplates.Where(x =>
                x.FileName == policyFile
                && x.Directory == directory);
            Assert.AreEqual(1, policies.Count());
            var policy = policies.First();
            Assert.AreEqual(ContentType.Xml, policy.Type);
            Assert.IsFalse(String.IsNullOrWhiteSpace(policy.XmlContent));
        }


        [TestMethod]
        public void TestResultContainsOperationWithFileLinkFor_HttbBinV2Put()
        {
            var template = _generatedTemplates.Single(x => x.FileName == HttpBinV2Filename);
            var policies = template.Content.SelectTokens(
                    "$..resources[?(@.type==\'Microsoft.ApiManagement/service/apis/operations/policies\')]")
                    .Where(x => x.Value<string>("name").Contains("'httpBinAPI-v2', '/', 'put'"));
            Assert.AreEqual(1, policies.Count());

            var policy = policies.First();
            JToken properties = policy["properties"];
            Assert.AreEqual("rawxml-link", properties.Value<string>("contentFormat"));
            Assert.AreEqual("[concat(parameters('repoBaseUrl'), '/api-Versioned-HTTP-bin-API/v2/api-Versioned-HTTP-bin-API.v2.put.policy.xml', parameters('TemplatesStorageAccountSASToken'))]", properties.Value<string>("policyContent"));
        }

        [TestMethod]
        public void TestResultContainsOperationWithFileLinkFor_HttbBinV2()
        {
            var template = _generatedTemplates.Single(x => x.FileName == HttpBinV2Filename);
            var policies = template.Content.SelectTokens(
                    "$..resources[?(@.type==\'Microsoft.ApiManagement/service/apis/policies\')]")
                    .Where(x => x.Value<string>("name").Contains("'httpBinAPI-v2', '/', 'policy'"));
            Assert.AreEqual(1, policies.Count());

            var policy = policies.First();
            JToken properties = policy["properties"];
            Assert.AreEqual("rawxml-link", properties.Value<string>("contentFormat"));
            Assert.AreEqual("[concat(parameters('repoBaseUrl'), '/api-Versioned-HTTP-bin-API/v2/api-Versioned-HTTP-bin-API.v2.policy.xml', parameters('TemplatesStorageAccountSASToken'))]", properties.Value<string>("policyContent"));
        }

        //TODO: Generate service-level-policies in GenerateTemplate
        //TODO: Generate files for service-level-policies

        private IEnumerable<JToken> GetPoliciesForProduct(string productFileName)
        {
            var product = _generatedTemplates.Single(x => x.FileName == productFileName);
            var policies = product.Content
                .SelectTokens("$.resources[*].resources[?(@.type==\'Microsoft.ApiManagement/service/products/policies\')]");
            return policies;
        }

        [TestMethod]
        public void TestResultContainsProductWithFileLinkFor_Starter()
        {
            var policy = GetPoliciesForProduct(ProductStarterFilename).First();

            JToken properties = policy["properties"];
            Assert.AreEqual("rawxml-link", properties.Value<string>("contentFormat"));
            Assert.AreEqual("[concat(parameters('repoBaseUrl'), '/product-starter/product-starter.policy.xml', parameters('TemplatesStorageAccountSASToken'))]", properties.Value<string>("policyContent"));
        }

        [TestMethod]
        public void TestResultContainsRepoBaseUrlParameterForProduct()
        {
            var template = _generatedTemplates.Single(x => x.FileName == ProductStarterFilename);
            var parameter = template.Content["parameters"]["repoBaseUrl"];
            Assert.IsNotNull(parameter);
            Assert.AreEqual("string", parameter.Value<string>("type"));
            Assert.IsNotNull(parameter["metadata"]);
            Assert.IsNotNull(parameter["metadata"]["description"]);
            Assert.AreEqual("Base URL of the repository", parameter["metadata"]["description"]);
        }

        [TestMethod]
        public void TestResultContainsTemplatesStorageAccountSASTokenParameterForProduct()
        {
            var template = _generatedTemplates.Single(x => x.FileName == ProductStarterFilename);
            var parameter = template.Content["parameters"]["TemplatesStorageAccountSASToken"];
            Assert.IsNotNull(parameter);
            Assert.AreEqual("string", parameter.Value<string>("type"));
            Assert.AreEqual(String.Empty, parameter.Value<string>("defaultValue"));
        }

        [TestMethod]
        public void TestResultContains_ProductStarterPolicyFile()
        {
            Assert.IsTrue(_generatedTemplates.Any(x =>
                x.FileName == "product-starter.policy.xml" &&
                x.Directory == @"product-starter" && x.Type == ContentType.Xml));
        }

        [TestMethod]
        public void TestResultContains_ProductUnlimited()
        {
            Assert.IsTrue(_generatedTemplates.Any(x =>
                x.FileName == "product-unlimited.template.json" &&
                x.Directory == @"product-unlimited"));
        }

        [TestMethod]
        public void TestResultContainsAPIFor_httpbinv1()
        {
            var api = _generatedTemplates.Single(x => x.FileName == HttpBinV1Filename);
            var noApis = api.Content.SelectTokens(JPathAPI)
                .Count(x => x.Value<string>("name").Contains("httpBinAPI"));
            Assert.AreEqual(1, noApis);
        }        

        [TestMethod]
        public void TestResultContainsAPIFor_httpbinv2()
        {
            var api = _generatedTemplates.Single(x => x.FileName == HttpBinV2Filename);
            var noApis = api.Content.SelectTokens(JPathAPI)
                .Count(x => x.Value<string>("name").Contains("httpBinAPI-v2"));
            Assert.AreEqual(1, noApis);
        }

        [TestMethod]
        public void TestResultContainsAPIFor_EchoV1()
        {
            var api = _generatedTemplates.Single(x => x.FileName == EchoFilename);
            var noApis = api.Content.SelectTokens(JPathAPI)
                .Count(x => x.Value<string>("name").Contains("echo-api"));
            Assert.AreEqual(1, noApis);
        }

        [TestMethod]
        public void TestResultContains_Service()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == ServiceFilename && x.Directory == String.Empty));
        }

        [TestMethod]
        public void TestResultContains_Subscriptions()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == "subscriptions.template.json" && x.Directory == String.Empty));
        }

        [TestMethod]
        public void TestResultContains_Users()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == "users.template.json" && x.Directory == String.Empty));
        }

        [TestMethod]
        public void TestResultContains_Groups()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == "groups.template.json" && x.Directory == String.Empty));
        }

        [TestMethod]
        public void TestResultContains_GroupsUsers()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == "groupsUsers.template.json" && x.Directory == String.Empty));
        }

        [TestMethod]
        public void TestResultContains_EchoV1()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == EchoFilename && x.Directory == @"api-Echo-API"));
        }

        [TestMethod]
        public void TestResultContainsParametersFor_httpbinv1()
        {
            var api = _generatedTemplates.Single(x => x.FileName == HttpBinV1Filename);
            var parameters = api.Content.SelectTokens(JPathParameters);

            Assert.AreEqual(7, parameters.Count());
        }

        [TestMethod]
        public void TestResultDoesNotDependOnServiceFor_httpbinv1()
        {
            var api = _generatedTemplates.Single(x => x.FileName == HttpBinV1Filename);
            var dependsOnService = api.Content.SelectTokens("$..dependsOn.[*]").Values<string>()
                .Where(x => x.StartsWith("[resourceId('Microsoft.ApiManagement/service',"));
            Assert.AreEqual(0, dependsOnService.Count());
        }

        [TestMethod]
        public void TestResultDoesNotDependOnServiceFor_httpbinVersionSet()
        {
            var api = _generatedTemplates.Single(x => x.FileName == HttpBinVersionSetFilename);
            var dependsOnService = api.Content.SelectTokens("$..dependsOn.[*]").Values<string>()
                .Where(x => x.StartsWith("[resourceId('Microsoft.ApiManagement/service',"));
            Assert.AreEqual(0, dependsOnService.Count());
        }

        [TestMethod]
        public void TestResultContainsVersionSetFor_httpbinVersionSet()
        {
            var api = _generatedTemplates.Single(x => x.FileName == HttpBinVersionSetFilename);
            var versionSets = api.Content.SelectTokens("$.resources[?(@.type==\'Microsoft.ApiManagement/service/api-version-sets\')]");
            var correctVersionSet = versionSets.Where(x => x.Value<string>("name").Contains("'versionset-httpbin-api'"));
            Assert.AreEqual(1, correctVersionSet.Count());
        }

        [TestMethod]
        public void TestResultContainsServiceFor_Service()
        {
            var api = _generatedTemplates.Single(x => x.FileName == ServiceFilename);
            var correctVersionSet = api.Content.SelectTokens("$.resources[?(@.type==\'Microsoft.ApiManagement/service\')]")
                .Where(x =>x.Value<string>("name").Contains("'service_PreDemoTest_name'"));
            Assert.AreEqual(1, correctVersionSet.Count());
        }

        [TestMethod]
        public void TestResultContainsVersionSetFor_httpbin()
        {
            Assert.AreEqual(1, _generatedTemplates.Count(x =>
                x.FileName == HttpBinVersionSetFilename &&
                x.Directory == @"api-Versioned-HTTP-bin-API"));
        }

        [TestMethod]
        public void TestResultContainsParametersFor_httpbinVersionSet()
        {
            var api = _generatedTemplates.Single(x => x.FileName == HttpBinVersionSetFilename);
            var parameters = api.Content.SelectTokens(JPathParameters);

            Assert.AreEqual(1, parameters.Count());
        }
    }
}