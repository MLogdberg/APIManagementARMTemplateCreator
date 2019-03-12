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
        private const string MasterTemplateFilename = "master.template.json";
        private const string ApiEchoApiDirectory = "api-Echo-API";
        private const string DeploymentResourceType = "Microsoft.Resources/deployments";
        private TemplatesGenerator _templatesGenerator;
        private string _sourceTemplate;
        private IList<GeneratedTemplate> _generatedTemplates;

        [TestInitialize()]
        public void Initialize()
        {
            _templatesGenerator = new TemplatesGenerator();
            _sourceTemplate = Utils.GetEmbededFileContent("APIManagementTemplate.Test.SamplesTemplate.template.json");
            _generatedTemplates = _templatesGenerator.Generate(_sourceTemplate, true, true, true, true, false);
        }

        [TestMethod]
        public void TestResultIsNotNull()
        {
            Assert.IsNotNull(_generatedTemplates);
        }

        [TestMethod]
        public void TestResultContainsCorrectNumberOfItems()
        {
            Assert.AreEqual(23, _generatedTemplates.Count);
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
                x.Directory == @"product-starter"));
        }

        [TestMethod]
        public void TestResultContains_ProductStarterWithoutAPI()
        {
            var product = _generatedTemplates.First(x => x.FileName == ProductStarterFilename && x.Directory == @"product-starter");
            Assert.IsNotNull(product);

            var apis = product.Content.SelectTokens($"$..resources[?(@.type=='Microsoft.ApiManagement/service/products/apis')]");

            Assert.AreEqual(0, apis.Count());


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
            AssertPolicyFile("api-Echo-API.create-resource.policy.xml", ApiEchoApiDirectory);
        }

        [TestMethod]
        public void TestResultContainsPolicyFileFor_Service()
        {
            AssertPolicyFile("service.policy.xml", "");
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

            AssertFileLink(policies.First(), "/api-Versioned-HTTP-bin-API/v2/api-Versioned-HTTP-bin-API.v2.put.policy.xml");
        }

        [TestMethod]
        public void TestResultContainsOperationWithFileLinkFor_HttbBinV2()
        {
            var template = _generatedTemplates.Single(x => x.FileName == HttpBinV2Filename);
            var policies = template.Content.SelectTokens(
                    "$..resources[?(@.type==\'Microsoft.ApiManagement/service/apis/policies\')]")
                    .Where(x => x.Value<string>("name").Contains("'httpBinAPI-v2', '/', 'policy'"));
            Assert.AreEqual(1, policies.Count());

            AssertFileLink(policies.First(), "/api-Versioned-HTTP-bin-API/v2/api-Versioned-HTTP-bin-API.v2.policy.xml");
        }


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

            AssertFileLink(policy, "/product-starter/product-starter.policy.xml");
        }

        [TestMethod]
        public void TestResultContainsProductPolicyFileFor_Starter()
        {
            var file = _generatedTemplates.Single(x => x.FileName == "product-starter.policy.xml");
            Assert.AreEqual(ContentType.Xml, file.Type);
            Assert.IsFalse(file.XmlContent.StartsWith("[concat(parameters('repoBaseUrl')"));
        }

        private static void AssertFileLink(JToken policy, string path)
        {
            JToken properties = policy["properties"];
            Assert.AreEqual("xml-link", properties.Value<string>("contentFormat"));
            Assert.AreEqual(
                $"[concat(parameters('repoBaseUrl'), '{path}', parameters('{TemplatesGenerator.TemplatesStorageAccountSASToken}'))]",
                properties.Value<string>("policyContent"));
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
        public void TestResultContainsAPIFor_echoAPI_with_apiversion_as_20180601preview()
        {
            //Because of a "bug" in Azure the api property subscriptionRequired is generated for all apiversion
            //even though it is only handled when deploying an api with apiversion 20180601preview or later
            var template = _generatedTemplates.With(Filename.Echo);
            var api = template.WithDirectResource(ResourceType.Api);
            Assert.AreEqual("2018-06-01-preview", api.Value(Arm.ApiVersion));
        }

        [TestMethod]
        public void TestResultContainsAPIFor_HttpBinV1_with_apiversion_as_20170301()
        {
            //Because of a "bug" in Azure the api property subscriptionRequired is generated for all apiversion
            //even though it is only handled when deploying an api with apiversion 20180601preview or later
            //An API without subscriptionRequired should keep the standard apiversion (20170301)
            var apiTemplate = _generatedTemplates.With(Filename.HttpBinV1);
            var api = apiTemplate.WithDirectResource(ResourceType.Api);
            Assert.AreEqual("2017-03-01", api.Value(Arm.ApiVersion));
        }

        [TestMethod]
        public void TestResultContainsTemplatesStorageAccountSASTokenParameterForProduct()
        {
            var template = _generatedTemplates.Single(x => x.FileName == ProductStarterFilename);
            var parameter = template.Content["parameters"][TemplatesGenerator.TemplatesStorageAccountSASToken];
            Assert.IsNotNull(parameter);
            Assert.AreEqual("securestring", parameter.Value<string>("type"));
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
        public void TestResultContainsAPIFor_EchoV1WithProductAPI()
        {
            var template = _generatedTemplates.Single(x => x.FileName == EchoFilename);

            var productApis = template.Content.SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/products/apis')]");
            var productApi = productApis.FirstOrDefault();
            Assert.IsNotNull(productApi);

            Assert.AreEqual("[concat(parameters('service_PreDemoTest_name'), '/', 'starter', '/', 'echo-api')]", productApi.Value<string>("name"));

            var dependsOn = productApi.Value<JArray>("dependsOn").Values<string>();
            Assert.AreEqual(1, dependsOn.Count());

            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service/apis', parameters('service_PreDemoTest_name'),'echo-api')]", 
                dependsOn.First());
        }

        [TestMethod]
        public void TestResultContainsAPIFor_EchoV1With2ProductAPI()
        {
            var template = _generatedTemplates.Single(x => x.FileName == EchoFilename);

            var productApis = template.Content.SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/products/apis')]");
            Assert.AreEqual(2, productApis.Count());
            Assert.IsTrue(productApis.Any(x => x.Value<string>("name") == "[concat(parameters('service_PreDemoTest_name'), '/', 'unlimited', '/', 'echo-api')]"));
            Assert.IsTrue(productApis.Any(x => x.Value<string>("name") == "[concat(parameters('service_PreDemoTest_name'), '/', 'starter', '/', 'echo-api')]"));
        }

        [TestMethod]
        public void TestResultContainsAPIMasterFileFor_Httpbin()
        {
            var api = _generatedTemplates.Single(x => x.Directory == "api-Versioned-HTTP-bin-API" && x.FileName == "api-Versioned-HTTP-bin-API.master.template.json");
            
            var deployments = api.Content.SelectTokens("$.resources[*]");
            Assert.AreEqual(3, deployments.Count());

        }

        [TestMethod]
        public void TestResultContainsMasterParametersFile()
        {
            var parameterTemplate = _generatedTemplates.Single(x => x.Directory == String.Empty && x.FileName == "master.parameters.json");
            var parameters = parameterTemplate.Content.SelectToken("$.parameters");
            Assert.AreNotEqual(0, parameters.Count());

            var serviceNameParameter = parameters.Cast<JProperty>().FirstOrDefault(x => x.Name == "service_PreDemoTest_name");
            Assert.IsNotNull(serviceNameParameter);
            Assert.AreEqual("PreDemoTest", serviceNameParameter.Value["value"].Value<string>());

            var subnetNameParameter = parameters.Cast<JProperty>().FirstOrDefault(x => x.Name == "service_PreDemoTest_virtualNetwork_subnetname");
            Assert.IsNotNull(subnetNameParameter);
            Assert.AreEqual(String.Empty, subnetNameParameter.Value["value"].Value<string>());
        }
        [TestMethod]
        public void TestResultContainsMasterParametersFileForVersionedHttpBin()
        {
            var parameterTemplate = _generatedTemplates.Single(x => x.Directory == "api-Versioned-HTTP-bin-API" && x.FileName == "api-Versioned-HTTP-bin-API.master.parameters.json");
            var parameters = parameterTemplate.Content.SelectToken("$.parameters");
            Assert.AreNotEqual(0, parameters.Count());

            var echoApiIsCurrent = parameters.Cast<JProperty>().FirstOrDefault(x => x.Name == "httpBinAPI_isCurrent");

            Assert.IsNotNull(echoApiIsCurrent);
            Assert.AreEqual(JTokenType.Boolean, echoApiIsCurrent.Value["value"].Type);
            Assert.AreEqual(true, echoApiIsCurrent.Value["value"].Value<bool>());

            Assert.IsNull(parameters.Cast<JProperty>().FirstOrDefault(x => x.Name == "repoBaseUrl"));
            Assert.IsNull(parameters.Cast<JProperty>().FirstOrDefault(x => x.Name == "apimServiceName"));

            // echo-api_isCurrent
        }

        [TestMethod]
        public void TestResultContainsApiMasterParametersFile_ForHttpBin()
        {
            var parameterTemplate = _generatedTemplates.Single(x => x.Directory == "api-Versioned-HTTP-bin-API" && x.FileName == "api-Versioned-HTTP-bin-API.master.parameters.json");
            var parameters = parameterTemplate.Content.SelectToken("$.parameters");
            Assert.AreNotEqual(0, parameters.Count());

            var serviceNameParameter = parameters.Cast<JProperty>().FirstOrDefault(x => x.Name == "service_PreDemoTest_name");

            Assert.IsNotNull(serviceNameParameter);
            Assert.AreEqual("PreDemoTest", serviceNameParameter.Value["value"].Value<string>());
        }

        [TestMethod]
        public void TestResultContains_Service()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == ServiceFilename && x.Directory == String.Empty));
        }


        [TestMethod]
        public void TestResultContains_PropertyForMyFunctionsKeyWithoutListSecrets()
        {
            GeneratedTemplate serviceTemplate = _generatedTemplates.Single(x => x.FileName == ServiceFilename && x.Directory == String.Empty);

            var property = serviceTemplate.Content.SelectTokens("$..resources[?(@.type=='Microsoft.ApiManagement/service/properties')]")
                .SingleOrDefault(x => x["name"].Value<string>().Contains("myfunctions-key"));
            Assert.IsNotNull(property);

            var value = property["properties"].Value<string>("value");
            Assert.IsFalse(value.StartsWith("[listsecrets("));

            Assert.AreEqual("[parameters('myfunctions-key')]", value);
        }

        [TestMethod]
        public void TestResultContains_VariableInServiceTemplate()
        {
            GeneratedTemplate serviceTemplate = _generatedTemplates.Single(x => x.FileName == ServiceFilename && x.Directory == String.Empty);

            var conf = serviceTemplate.Content["variables"]["virtualNetworkConfiguration"];
            Assert.IsNotNull(conf);
            Assert.IsNotNull(conf["subnetResourceId"]);
            Assert.IsNotNull(conf["vnetid"]);
            Assert.IsNotNull(conf["subnetname"]);
        }

        [TestMethod]
        public void TestResultContains_VirtualNetworkConfigurationParameters()
        {
            AssertParameter("service_PreDemoTest_virtualNetwork_subnetResourceId", "string", "/subscriptions/c107df29-a4af-4bc9-a733-f88f0eaa4296/resourceGroups/PreDemoTest/providers/Microsoft.Network/virtualNetworks/Network/subnets/default");
            AssertParameter("service_PreDemoTest_virtualNetwork_vnetid", "string", "00000000-0000-0000-0000-000000000000");
            AssertParameter("service_PreDemoTest_virtualNetwork_subnetname", "string", "");
        }

        [TestMethod]
        public void TestResultContains_ParameterForMyFunctionsKey()
        {
            AssertParameter("myfunctions-key", "securestring", "");
        }

        private void AssertParameter(string name, string type, string defaultValue)
        {
            GeneratedTemplate serviceTemplate =
                _generatedTemplates.Single(x => x.FileName == ServiceFilename && x.Directory == String.Empty);

            var parameter = serviceTemplate.Content.SelectToken($"$.parameters.{name}");
            Assert.IsNotNull(parameter);

            Assert.AreEqual(type, parameter.Value<string>("type"));
            Assert.AreEqual(defaultValue, parameter.Value<string>("defaultValue"));
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
        public void TestResultContains_GroupsWithNoExternalDependency()
        {
            GeneratedTemplate groups = _generatedTemplates.FirstOrDefault(x => x.FileName == "groups.template.json" && x.Directory == String.Empty);
            Assert.IsNotNull(groups);

            var dependencies = groups.Content.SelectTokens("$..dependsOn[*]");

            Assert.AreEqual(0, dependencies.Count());

            Assert.AreEqual(1, groups.ExternalDependencies.Count);
            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service', parameters('service_PreDemoTest_name'))]", groups.ExternalDependencies.First());
        }

        [TestMethod]
        public void TestResultContains_GroupsUsers()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == "groupsUsers.template.json" && x.Directory == String.Empty));
        }

        [TestMethod]
        public void TestResultContains_EchoV1()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == EchoFilename && x.Directory == ApiEchoApiDirectory));
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
            var services = api.Content.SelectTokens("$.resources[?(@.type==\'Microsoft.ApiManagement/service\')]")
                .Where(x => x.Value<string>("name").Contains("'service_PreDemoTest_name'"));
            Assert.AreEqual(1, services.Count());
        }


        [TestMethod]
        public void TestResultContainsPolicyFor_Service()
        {
            var service = _generatedTemplates.Single(x => x.FileName == ServiceFilename);
            var policy = service.Content.SelectTokens("$..resources[?(@.type==\'Microsoft.ApiManagement/service/policies\')]");
            Assert.AreEqual(1, policy.Count());
        }

        [TestMethod]
        public void TestResultContainsPolicyWithFileLinkFor_Service()
        {
            var service = _generatedTemplates.Single(x => x.FileName == ServiceFilename);
            var policies = service.Content.SelectTokens("$..resources[?(@.type==\'Microsoft.ApiManagement/service/policies\')]");
            Assert.AreEqual(1, policies.Count());
            JToken policy = policies.First();
            Assert.AreEqual("2018-01-01", policy.Value<string>("apiVersion"));
            AssertFileLink(policy, "/service.policy.xml");
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

        [TestMethod]
        public void TestResultContainsMasterTemplateJson()
        {
            var masterTemplate = GetMasterTemplate();
            Assert.IsNotNull(masterTemplate);
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateWithoutAPIs()
        {
            var masterTemplate = GetMasterTemplate();
            Assert.IsNotNull(masterTemplate);
            var apis = masterTemplate.Content.SelectTokens($"$..resources[?(@.type=='{DeploymentResourceType}')]")
                .Where(x => x.Value<string>("name").StartsWith("api-"));
            Assert.AreEqual(0, apis.Count());
        }

        private GeneratedTemplate GetMasterTemplate()
        {
            return _generatedTemplates.FirstOrDefault(x => x.FileName == MasterTemplateFilename && x.Directory == String.Empty);
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateJsonWith_ServiceTemplate()
        {
            AssertMasterTemplateDeployment(String.Empty, "service.template.json");
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateJsonWith_UnlimitedProduct()
        {
            var deployment = AssertMasterTemplateDeployment("/product-unlimited", "product-unlimited.template.json", false);
            var dependsOn = deployment["dependsOn"];
            Assert.IsNotNull(dependsOn);
            Assert.AreEqual(1, dependsOn.Count());
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateParameter_repoBaseUrl()
        {
            var template = GetMasterTemplate();
            var repoBaseUrl = template.Content["parameters"]["repoBaseUrl"];
            Assert.IsNotNull(repoBaseUrl);
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateParameter__artifactsLocationSasToken()
        {
            var template = GetMasterTemplate();
            var repoBaseUrl = template.Content["parameters"][TemplatesGenerator.TemplatesStorageAccountSASToken];
            Assert.IsNotNull(repoBaseUrl);
        }

        [TestMethod]
        public void TestResultContainsEchoApiOperationWithDependencyToEchoApi()
        {
            var api = _generatedTemplates.Single(x => x.FileName == EchoFilename);
            var operation = api.Content.SelectTokens("$..resources[?(@.type=='Microsoft.ApiManagement/service/apis/operations')]").FirstOrDefault(x => x.Value<string>("name").Contains("'create-resource'"));
            Assert.IsNotNull(operation);

            var dependsOn = operation.Value<JArray>("dependsOn");
            Assert.AreNotEqual(0, dependsOn.Count());
        }

      
        private JToken AssertMasterTemplateDeployment(string path, string fileName, bool checkRepoBaseUrl = true)
        {
            var template = GetMasterTemplate();
            var deployments = template.Content.SelectTokens("$.resources[?(@.type=='Microsoft.Resources/deployments')]")
                .Where(x => x.Value<string>("name").Contains(fileName));
            Assert.AreEqual(1, deployments.Count());
            var deployment = deployments.First();
            var properties = deployment["properties"];
            Assert.IsNotNull(properties);

            var mode = properties.Value<string>("mode");
            Assert.AreEqual("Incremental", mode);

            var uri = deployments.First()["properties"]["templateLink"].Value<string>("uri");
            Assert.AreEqual(
                $"[concat(parameters('repoBaseUrl'), '{path}/{fileName}', parameters('{TemplatesGenerator.TemplatesStorageAccountSASToken}'))]", uri);

            var contentVersion = deployments.First()["properties"]["templateLink"].Value<string>("contentVersion");
            Assert.AreEqual("1.0.0.0", contentVersion);

            var parameters = deployments.First()["properties"]["parameters"];
            Assert.IsNotNull(parameters);
            if (checkRepoBaseUrl)
            {
                var repoBaseUrl = parameters["repoBaseUrl"];
                Assert.IsNotNull(repoBaseUrl);
            }
            return deployment;
        }
    }
}