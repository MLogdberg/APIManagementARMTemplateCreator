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
    public class TemplatesGeneratorTestsWithListApiInProduct
    {
        private TemplatesGenerator _templatesGenerator;
        private string _sourceTemplate;
        private IList<GeneratedTemplate> _generatedTemplates;

        [TestInitialize()]
        public void Initialize()
        {
            _templatesGenerator = new TemplatesGenerator();
            _sourceTemplate = Utils.GetEmbededFileContent("APIManagementTemplate.Test.SamplesTemplate.templateParameters.json");
            _generatedTemplates = _templatesGenerator.Generate(_sourceTemplate, false, false, listApiInProduct:true);
        }

        [TestMethod]
        public void TestResultContainsUnlimitedProductWithParameter()
        {
            var productTemplate = _generatedTemplates.With(Filename.ProductUnlimited);
            Assert.IsNotNull(productTemplate);
            var parameter = productTemplate.Content.Index(Arm.Parameters)["apis_in_product_unlimited"];
            Assert.IsNotNull(parameter);
            Assert.AreEqual("array", parameter.Value(Arm.Type));
            var defaultValue = parameter.ValueWithType<JArray>(Arm.DefaultValue);
            Assert.IsNotNull(defaultValue);
            Assert.AreEqual(1, defaultValue.Count);
            Assert.AreEqual("tfs", defaultValue[0].Value<string>());
        }

        [TestMethod]
        public void TestResultContainsUnlimitedProductWithApis()
        {
            var productTemplate = _generatedTemplates.With(Filename.ProductUnlimited);
            Assert.IsNotNull(productTemplate);
            var productApis = productTemplate.WithDirectResources(ResourceType.ProductApi);
            Assert.AreEqual(1, productApis.Count());
            Assert.IsTrue(productTemplate.ExternalDependencies.Contains(
                "[resourceId('Microsoft.ApiManagement/service/products', parameters('apimServiceName'), parameters('apis_in_product_unlimited'))]"));
        }

        [TestMethod]
        public void TestResultContainsUnlimitedProductWithApisName()
        {
            Assert.AreEqual(
                "[concat(parameters('apimServiceName'), '/' ,parameters('product_unlimited_name'), '/', parameters('apis_in_product_unlimited')[copyIndex()])]",
                GetProductApi().Value(Arm.Name));
        }

        [TestMethod]
        public void TestResultContainsUnlimitedProductWithApisApiVersion()
        {
            Assert.AreEqual("2017-03-01", GetProductApi().Value(Arm.ApiVersion));
        }

        [TestMethod]
        public void TestResultContainsUnlimitedProductWithApisCopy()
        {
            var copy = GetProductApi().Index(Arm.Copy);
            Assert.IsNotNull(copy);
            Assert.AreEqual("apicopy", copy.Value(Arm.Name));
            Assert.AreEqual("[length(parameters('apis_in_product_unlimited'))]", 
                copy.Value(Arm.Count));
        }

        private JToken GetProductApi()
        {
            var productTemplate = _generatedTemplates.With(Filename.ProductUnlimited);
            Assert.IsNotNull(productTemplate);
            var productApis = productTemplate.WithDirectResources(ResourceType.ProductApi);
            Assert.AreEqual(1, productApis.Count());
            return productApis.First();
        }
    }
}