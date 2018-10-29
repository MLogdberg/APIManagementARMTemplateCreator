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
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == "api-Versioned-HTTP-bin-API.v1.template.json" && x.Directory== @"api-Versioned-HTTP-bin-API\v1"));
        }

        [TestMethod]
        public void TestResultContains_httpbinv2()
        {
            Assert.IsTrue(_generatedTemplates.Any(x =>
                x.FileName == "api-Versioned-HTTP-bin-API.v2.template.json" &&
                x.Directory == @"api-Versioned-HTTP-bin-API\v2"));
        }

        [TestMethod]
        public void TestResultContains_EchoV1()
        {
            Assert.IsTrue(_generatedTemplates.Any(x => x.FileName == "api-Echo-API.v1.template.json" && x.Directory == @"api-Echo-API\v1"));
        }

    }
}