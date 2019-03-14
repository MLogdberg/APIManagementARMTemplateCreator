using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class TemplatesGeneratorTestsWithoutApiVersionSetId
    {
        private IResourceCollector _collector;
        private JObject _template = null;
        private IList<GeneratedTemplate> _generatedTemplates;

        [TestInitialize()]
        public void Initialize()
        {
            this._collector = new MockResourceCollector("WithoutApiVersionSetId");
            _generatedTemplates = GetGeneratedTemplates().GetAwaiter().GetResult();
        }

        private async Task<JObject> GetTemplate(bool exportProducts = false, bool parametrizePropertiesOnly = true,
            bool replaceSetBackendServiceBaseUrlAsProperty = false, bool fixedServiceNameParameter = false,
            bool createApplicationInsightsInstance = false, bool exportSwaggerDefinition = false)
        {
            if (this._template != null)
                return this._template;
            var generator = new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest",
                "maloapimtestclean", false, exportProducts, true, parametrizePropertiesOnly, this._collector,
                replaceSetBackendServiceBaseUrlAsProperty, fixedServiceNameParameter,
                createApplicationInsightsInstance, exportSwaggerDefinition: exportSwaggerDefinition);
            this._template = await generator.GenerateTemplate();
            return this._template;
        }

        [TestMethod]
        public async Task TestApiTemplateShouldNotHaveSameResourcesAsApiSwaggerTemplate()
        {
            var names = GetApiResourceNames(_generatedTemplates, Filename.TFSTemplate);
            var swaggerNames = GetApiResourceNames(_generatedTemplates, Filename.TFSSwaggerTemplate);
            var sameNames = names.Intersect(swaggerNames);
            sameNames.Should().BeEmpty();
        }

        private static IEnumerable<string> GetApiResourceNames(IList<GeneratedTemplate> generatedTemplates, Filename template)
        {
            var apiTemplate = generatedTemplates.With(template).WithDirectResource(ResourceType.Api);
            var resources = apiTemplate.ValueWithType<JArray>(Arm.Resources);
            Assert.IsNotNull(resources);
            return resources.Select(x => $"{x.Value(Arm.Type)}_{x.Value(Arm.Name)}");
        }

        private async Task<IList<GeneratedTemplate>> GetGeneratedTemplates()
        {
            var template = await GetTemplate(exportProducts: true, fixedServiceNameParameter: true,
                createApplicationInsightsInstance: true, exportSwaggerDefinition: true,
                replaceSetBackendServiceBaseUrlAsProperty: true);

            var templatesGenerator = new TemplatesGenerator();
            var generatedTemplates = templatesGenerator.Generate(template.ToString(), true, true,
                true, true, false, separateSwaggerFile: true);
            return generatedTemplates;
        }
    }
}