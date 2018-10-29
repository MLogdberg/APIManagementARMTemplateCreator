using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class TemplatesGeneratorTests
    {
        private const string HttpBinV1Filename = "api-Versioned-HTTP-bin-API.v1.template.json";
        private const string HttpBinV2Filename = "api-Versioned-HTTP-bin-API.v2.template.json";
        private const string EchoFilename = "api-Echo-API.template.json";
        private const string JPathAPI = "$.resources[?(@.type=='Microsoft.ApiManagement/service/apis')]";
        private TemplatesGenerator _templatesGenerator;
        private string _sourceTemplate;
        private IList<GeneratedTemplate> _generatedTemplates;

        [TestInitialize()]
        public void Initialize()
        {
            _templatesGenerator = new TemplatesGenerator();
            _sourceTemplate = Utils.GetEmbededFileContent("APIManagementTemplate.Test.SamplesTemplate.template.json");
            _generatedTemplates = _templatesGenerator.Generate(_sourceTemplate);
        }

        [TestMethod]
        public void TestResultIsNotNull()
        {
            Assert.IsNotNull(_generatedTemplates);
        }

        [TestMethod]
        public void TestResultContains3Items()
        {
            Assert.AreEqual(3, _generatedTemplates.Count);
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
        public void TestResultContains_EchoV1()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == EchoFilename && x.Directory == @"api-Echo-API"));
        }

    }
}