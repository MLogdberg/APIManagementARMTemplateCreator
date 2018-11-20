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
    public class TemplatesGeneratorTestsWithSeparatePolicyFileAsFalse
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
        private TemplatesGenerator _templatesGenerator;
        private string _sourceTemplate;
        private IList<GeneratedTemplate> _generatedTemplates;

        [TestInitialize()]
        public void Initialize()
        {
            _templatesGenerator = new TemplatesGenerator();
            _sourceTemplate = Utils.GetEmbededFileContent("APIManagementTemplate.Test.SamplesTemplate.template.json");
            _generatedTemplates = _templatesGenerator.Generate(_sourceTemplate, false, false);
        }

        [TestMethod]
        public void TestResultIsNotNull()
        {
            Assert.IsNotNull(_generatedTemplates);
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateJson()
        {
            var masterTemplate = GetMasterTemplate();
            Assert.IsNotNull(masterTemplate);
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
        public void TestResultContainsMasterTemplateJsonWith_ApiCalculator()
        {
            AssertMasterTemplateDeployment($"/{ApiEchoApiDirectory}", EchoFilename, false);
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateJsonWith_UnlimitedProduct()
        {
            var deployment = AssertMasterTemplateDeployment("/product-unlimited", "product-unlimited.template.json");
            var dependsOn = deployment["dependsOn"];
            Assert.IsNotNull(dependsOn);
            Assert.AreEqual(1, dependsOn.Count());
        }

        [TestMethod]
        public void TestResultContainsMasterTemplateJsonWith_HttpBinV2()
        {
            AssertMasterTemplateDeployment("/api-Versioned-HTTP-bin-API/v2", "api-Versioned-HTTP-bin-API.v2.template.json");
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
                $"[concat(parameters('repoBaseUrl'), '{path}/{fileName}', parameters('_artifactsLocationSasToken'))]", uri);

            var contentVersion = deployments.First()["properties"]["templateLink"].Value<string>("contentVersion");
            Assert.AreEqual("1.0.0.0", contentVersion);

            var parameters = deployments.First()["properties"]["parameters"];
            Assert.IsNotNull(parameters);
            return deployment;
        }
    }
}