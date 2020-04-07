using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class WithoutApiVersionSetIdTests
    {
        private IResourceCollector collector;

        [TestInitialize()]
        public void Initialize()
        {
            this.collector = new MockResourceCollector("WithoutApiVersionSetId");

        }

        private JObject _template = null;

        private JObject GetTemplate(bool exportProducts = false, bool parametrizePropertiesOnly = true,
            bool replaceSetBackendServiceBaseUrlAsProperty = false, bool fixedServiceNameParameter = false,
            bool createApplicationInsightsInstance = false, bool exportSwaggerDefinition = false, bool exportGroups = false)
        {
            if (this._template != null)
                return this._template;
            var generator = new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest",
                "maloapimtestclean", exportGroups, exportProducts, true, parametrizePropertiesOnly, this.collector,
                replaceSetBackendServiceBaseUrlAsProperty, fixedServiceNameParameter,
                createApplicationInsightsInstance, exportSwaggerDefinition: exportSwaggerDefinition);
            this._template = generator.GenerateTemplate().GetAwaiter().GetResult();
            return this._template;
        }


        private JToken GetResourceFromTemplate(ResourceType resourceType, bool createApplicationInsightsInstance = false,
            bool parametrizePropertiesOnly = true, bool fixedServiceNameParameter = false, bool exportSwaggerDefinition = false, bool replaceSetBackendServiceBaseUrlAsProperty=false)
        {
            var template = GetTemplate(true, parametrizePropertiesOnly,
                fixedServiceNameParameter: fixedServiceNameParameter,
                createApplicationInsightsInstance: createApplicationInsightsInstance, exportSwaggerDefinition: exportSwaggerDefinition, replaceSetBackendServiceBaseUrlAsProperty:replaceSetBackendServiceBaseUrlAsProperty);
            return template.WithDirectResource(resourceType);
        }

        [TestMethod]
        public void TestApiVersionSetIdForApiIsNotSet()
        {
            JToken api = GetResourceFromTemplate(ResourceType.Api, false);
            Assert.IsNotNull(api);

            var versionSetId = api.Index(Arm.Properties).Value(Arm.ApiVersionSetId);
            Assert.IsNull(versionSetId);
        }

        [TestMethod]
        public void TestApiVersionForApiIs20190101()
        {
            JToken api = GetResourceFromTemplate(ResourceType.Api, false);
            Assert.IsNotNull(api);

            var apiVersion = api.Value(Arm.ApiVersion);
            Assert.AreEqual("2019-01-01", apiVersion);
        }



        [TestMethod]
        public void TestProductContainsPolicy()
        {
            IEnumerable<JToken> policies =
                GetSubResourceFromTemplate(ResourceType.Product, ResourceType.ProductPolicy, false);

            Assert.AreEqual(1, policies.Count());
        }

        [TestMethod]
        public void TestProductContains4Groups()
        {
            var template = GetTemplate(true, true, false, true, false, false, true);
            var productGroups = template.WithResources(ResourceType.ProductGroup);
            Assert.IsNotNull(productGroups);

            Assert.AreEqual(4, productGroups.Count());
            var productGroup = productGroups.SingleOrDefault(x => x.Value(Arm.Name).Contains("office-services"));
            Assert.IsNotNull(productGroup);
            Assert.AreEqual("[concat(parameters('apimServiceName'), '/', 'unlimited', '/', 'office-services')]",
                productGroup.Value(Arm.Name));
            JToken properties = productGroup.Index(Arm.Properties);
            Assert.AreEqual("Office Services", properties.Value(Arm.DisplayName));
            Assert.AreEqual(false, properties.ValueWithType<bool>(Arm.BuiltIn));
            Assert.AreEqual("custom", properties.Value(Arm.Type));
        }

        [TestMethod]
        public void TestContains1Group()
        {
            var template = GetTemplate(true, true, false, true, false, false, true);
            var policyGroup = template.WithResource(ResourceType.Group);
            Assert.IsNotNull(policyGroup);

            Assert.AreEqual("[concat(parameters('apimServiceName'), '/office-services')]",
                policyGroup.Value(Arm.Name));
            JToken properties = policyGroup.Index(Arm.Properties);
            Assert.AreEqual("Office Services", properties.Value(Arm.DisplayName));
            Assert.AreEqual(false, properties.ValueWithType<bool>(Arm.BuiltIn));
            Assert.AreEqual("custom", properties.Value(Arm.Type));
        }


        [TestMethod]
        public void TestContainsCertificate()
        {
            JToken certificate = GetResourceFromTemplate(ResourceType.Certificate, false, true);
            Assert.IsNotNull(certificate);

            var properties = certificate.Index(Arm.Properties);
            Assert.IsNotNull(properties);

            var data = properties.Value(Arm.Data);
            Assert.IsNotNull(data);
            Assert.AreEqual("[parameters('certificate_MyCertificate_data')]", data);

            var password = properties.Value(Arm.Password);
            Assert.IsNotNull(password);
            Assert.AreEqual("[parameters('certificate_MyCertificate_password')]", password);

            Assert.AreEqual(2, properties.Children().Count());
        }

        [TestMethod]
        public void TestServiceNameParameterIsApimServiceNameWhenUseFixedServiceNameParameter()
        {
            var service = GetResourceFromTemplate(ResourceType.Service, false, fixedServiceNameParameter: true);
            Assert.AreEqual("[parameters('apimServiceName')]", service.Value(Arm.Name));
        }

        [TestMethod]
        public void TestServiceHasCorrectVirtualNetworkType()
        {
            var service = GetResourceFromTemplate(ResourceType.Service, false, fixedServiceNameParameter: true);
            Assert.AreEqual("[parameters('apimServiceName_virtualNetworkType')]",
                service.Index(Arm.Properties).Value(Arm.VirtualNetworkType));
        }

        [TestMethod]
        public void TestServiceHasCorrectVirtualNetworkConfiguration()
        {
            var service = GetResourceFromTemplate(ResourceType.Service, false, fixedServiceNameParameter: true);
            var configuration = service.Index(Arm.Properties).Value(Arm.VirtualNetworkConfiguration);
            Assert.IsNotNull(configuration);
            Assert.AreEqual(
                "[if(not(equals(parameters('apimServiceName_virtualNetworkType'), 'None')), variables('virtualNetworkConfiguration'), json('null'))]",
                configuration);
        }

        [TestMethod]
        public void TestServiceHasVariableForvirtualNetworkConfiguration()
        {
            var template = GetTemplate(fixedServiceNameParameter: true);
            var variables = template.Index(Arm.Variables);

            Assert.IsNotNull(variables);
            Assert.AreEqual(1, variables.Count());

            var configuration = variables.Index(Arm.VirtualNetworkConfiguration);
            Assert.IsNotNull(configuration);
            Assert.AreEqual("[if(empty(parameters('apimServiceName_virtualNetwork_subnetResourceId')), json('null'), parameters('apimServiceName_virtualNetwork_subnetResourceId'))]",
                configuration.Value(Arm.SubnetResourceId));
            Assert.AreEqual("[if(empty(parameters('apimServiceName_virtualNetwork_vnetid')), json('null'), parameters('apimServiceName_virtualNetwork_vnetid'))]",
                configuration.Value(Arm.VnetId));
            Assert.AreEqual(
                "[if(equals(parameters('apimServiceName_virtualNetwork_subnetname'), ''), json('null'), parameters('apimServiceName_virtualNetwork_subnetname'))]",
                configuration.Value(Arm.SubnetName));
        }

        [TestMethod]
        public void TestServiceContainsAppInsightsLoggerWhenCreateApplicationInsightsInstanceIsTrue()
        {
            IEnumerable<JToken> loggers = GetSubResourceFromTemplate(ResourceType.Service, ResourceType.Logger, true);

            Assert.AreEqual(3, loggers.Count());

            var logger = loggers.First(x => x.Value(Arm.Name).Contains("applicationInsights"));
            Assert.AreEqual(
                "[concat(parameters('service_ibizmalo_name'), '/', parameters('service_ibizmalo_applicationInsights'))]",
                logger.Value(Arm.Name));
            Assert.AreEqual("Microsoft.ApiManagement/service/loggers", logger.Value(Arm.Type));
            var properties = logger.Index(Arm.Properties) as JObject;
            Assert.IsNotNull(properties);
            Assert.AreEqual("applicationInsights", properties.Value(Arm.LoggerType));
            var resourceId = properties.GetValue("resourceId");
            Assert.IsNull(resourceId);

            var credentials = properties.Index(Arm.Credentials);
            Assert.IsNotNull(credentials);

            Assert.AreEqual(
                "[reference(resourceId('Microsoft.Insights/components', parameters('service_ibizmalo_applicationInsights')), '2014-04-01').InstrumentationKey]",
                credentials.Value(Arm.InstrumentationKey));

            var dependsOn = logger.DependsOn();
            Assert.AreEqual(2, dependsOn.Count());
            Assert.AreEqual(
                "[resourceId('Microsoft.Insights/components',parameters('service_ibizmalo_applicationInsights'))]",
                dependsOn.Last());

        }

        [TestMethod]
        public void TestServiceContainsAppInsightsLoggerWhenCreateApplicationInsightsInstanceIsFalse()
        {
            IEnumerable<JToken> loggers = GetSubResourceFromTemplate(ResourceType.Service, ResourceType.Logger, false);

            Assert.AreEqual(3, loggers.Count());

            var logger = loggers.First(x => x.Value(Arm.Name).Contains("parameters('service_ibizmalo_applicationInsights')"));
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', parameters('service_ibizmalo_applicationInsights'))]",
                logger.Value(Arm.Name));
            Assert.AreEqual("Microsoft.ApiManagement/service/loggers", logger.Value(Arm.Type));
            var properties = logger.Index(Arm.Properties);
            Assert.IsNotNull(properties);
            Assert.AreEqual("applicationInsights", properties.Value(Arm.LoggerType));

            var credentials = properties.Index(Arm.Credentials);
            Assert.IsNotNull(credentials);

            Assert.AreEqual("{{Logger-Credentials-5b5dbaa35a635f22ac9db432}}",
                credentials.Value(Arm.InstrumentationKey));
        }

        [TestMethod]
        public void TestServiceContainsAzureEventhubLogger()
        {
            IEnumerable<JToken> loggers = GetSubResourceFromTemplate(ResourceType.Service, ResourceType.Logger, false);

            Assert.AreEqual(3, loggers.Count());

            var logger = loggers.First(x => x.Value(Arm.Name).Contains("bpst-apim-l-6a12e"));
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', 'bpst-apim-l-6a12e')]",
                logger.Value(Arm.Name));
            Assert.AreEqual("Microsoft.ApiManagement/service/loggers", logger.Value(Arm.Type));
            var properties = logger.Index(Arm.Properties);
            Assert.IsNotNull(properties);
            Assert.AreEqual("azureEventHub", properties.Value(Arm.LoggerType));

            var credentials = properties.Index(Arm.Credentials);
            Assert.IsNotNull(credentials);

            Assert.AreEqual("[parameters('logger_bpst-apim-l-6a12e_credentialName')]",
                credentials.Value(Arm.Name));
            Assert.AreEqual("[parameters('logger_bpst-apim-l-6a12e_connectionString')]",
                credentials.Value(Arm.ConnectionString));
        }

        private IEnumerable<JToken> GetSubResourceFromTemplate(ResourceType resourceType, ResourceType subResourceType,
            bool createApplicationInsightsInstance = false, bool parametrizePropertiesOnly = true, bool exportSwaggerDefinition =false)
        {
            JToken resource = GetResourceFromTemplate(resourceType, createApplicationInsightsInstance,
                parametrizePropertiesOnly, exportSwaggerDefinition:exportSwaggerDefinition);
            return resource.WithDirectResources(subResourceType);
        }

        [TestMethod]
        public void TestProductContainsPolicyWithCorrectName()
        {
            var policy = GetSubResourceFromTemplate(ResourceType.Product, ResourceType.ProductPolicy, false).First();
            var name = policy.Value(Arm.Name);
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', 'unlimited', '/', 'policy')]", name);
        }

        [TestMethod]
        public void TestApiContainsPolicyReplacedSetBaseUrl()
        {
            var template = GetTemplate();
            var apiPolicies = template.WithResources(ResourceType.ApiPolicy);
            var policy = apiPolicies.FirstOrDefault();
            Assert.IsNotNull(policy);
            var policyContent = policy.Index(Arm.Properties)?.Value(Arm.PolicyContent);
            Assert.IsTrue(policyContent.StartsWith("[Concat('"));
        }

        [TestMethod]
        public void TestApiContainsPolicyReplacedSetBaseUrlAsPropertyWhenReplaceSetBaseUrlAsPropertyIsTrue()
        {
            var template = GetTemplate(replaceSetBackendServiceBaseUrlAsProperty: true);
            var apiPolicies = template.WithResources(ResourceType.ApiPolicy);
            var policy = apiPolicies.FirstOrDefault();
            Assert.IsNotNull(policy);
            var policyContent = policy.Index(Arm.Properties)?.Value(Arm.PolicyContent);

            Assert.IsFalse(policyContent.StartsWith("[Concat('"));
            Assert.IsTrue(policyContent.Contains("{{api_tfs_backendurl}}"));

        }

        [TestMethod]
        public void TestApiContainsPropertyWhenReplaceSetBaseUrlAsPropertyIsTrue()
        {
            var template = GetTemplate(replaceSetBackendServiceBaseUrlAsProperty: true);
            var property = template.WithDirectResources(ResourceType.Property)
                .SingleOrDefault(x => x.Value(Arm.Name).Contains("api_tfs_backendurl"));
            Assert.IsNotNull(property);
        }

        [TestMethod]
        public void TestServiceContainsPropertyForEnvironment()
        {
            var template = GetTemplate();
            var property = template.WithDirectResources(ResourceType.Property)
                .SingleOrDefault(x => x.Value(Arm.Name).Contains("environment"));
            Assert.IsNotNull(property);
        }

        [TestMethod]
        public void TestProductContainsPolicyThatDependsOnProduct()
        {
            var policy = GetSubResourceFromTemplate(ResourceType.Product, ResourceType.ProductPolicy, false).First();

            var dependsOn = policy.DependsOn();
            Assert.IsTrue(dependsOn.Any(x =>
                x == "[resourceId('Microsoft.ApiManagement/service/products', parameters('service_ibizmalo_name'), 'unlimited')]"));
        }

        [TestMethod]
        public void TestServiceContainsPolicyWithCorrectName()
        {
            var policy = GetSubResourceFromTemplate(ResourceType.Service, ResourceType.ServicePolicy, false).First();

            Assert.IsNotNull(policy);
            var name = policy.Value(Arm.Name);
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', 'policy')]", name);
        }

        [TestMethod]
        public void TestServiceContainsOneParameterWithServiceName()
        {
            //The background to this test is that the name part of the id and the name of the service ARM template could have
            //upper case letter but in all other places it is in lowercase.
            //This caused the bug where we got two parameters service_ibizmalo_name and service_Ibizmalo_name (if the name of the service is Ibizmalo)
            var template = GetTemplate();
            var parameters = template.SelectToken("$.parameters").Cast<JProperty>();
            var serviceNameParameters = parameters.Where(p => p.Name.ToLowerInvariant() == "service_ibizmalo_name");

            Assert.AreEqual(1, serviceNameParameters.Count());
        }


        [TestMethod]
        public void TestServiceContainsPropertyForLogger()
        {
            var template = GetTemplate();
            var property = template.WithDirectResources(ResourceType.Property)
                .SingleOrDefault(x => x.Value(Arm.Name).Contains("5b5dbaa35a635f22ac9db431"));

            Assert.IsNotNull(property);
            var hasServiceDependency = property.DependsOn().Any(x => x ==
                                                          "[resourceId('Microsoft.ApiManagement/service', parameters('service_ibizmalo_name'))]");
            Assert.IsTrue(hasServiceDependency);
        }

        [TestMethod]
        public void TestServiceDoesNotContainPropertyForLoggerWhenCreateApplicationInsightsInstanceIsTrue()
        {
            var template = GetTemplate(createApplicationInsightsInstance: true);
            var property = template.WithResources(ResourceType.Property)
                .SingleOrDefault(x => x.Value(Arm.Name).Contains("5b5dbaa35a635f22ac9db431"));

            Assert.IsNull(property);
        }

        [TestMethod]
        public void TestServiceContainsPropertyForBackend()
        {
            var template = GetTemplate();
            var property = template.WithResources(ResourceType.Property)
                .SingleOrDefault(x => x.Value(Arm.Name).Contains("myfunctions-key"));

            Assert.IsNotNull(property);
            var dependsOn = property.DependsOn();
            var hasServiceDependency = dependsOn.Any(x => x ==
              "[resourceId('Microsoft.ApiManagement/service', parameters('service_ibizmalo_name'))]");
            Assert.IsTrue(hasServiceDependency);
        }

        [TestMethod]
        public void TestServiceContainsPolicyWithCorrectDependsOn()
        {
            var policy = GetSubResourceFromTemplate(ResourceType.Service, ResourceType.ServicePolicy, false).First();

            Assert.IsNotNull(policy);
            var dependsOn = policy.DependsOn();
            Assert.AreEqual(1, dependsOn.Count());
            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service', parameters('service_ibizmalo_name'))]",
                dependsOn.First());
        }

        [TestMethod]
        public void TestServiceContainsSchema()
        {
            var schema = GetSubResourceFromTemplate(ResourceType.Api, ResourceType.Schema, false).First();
            var name = schema.Value(Arm.Name);

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', 'tfs', '/', '5b7572981142a50298c7f4a6')]",
                name);
        }

        [TestMethod]
        public void TestServiceContainsBackend()
        {
            var backend = GetResourceFromTemplate(ResourceType.Backend, false, true);
            var name = backend.Value(Arm.Name);

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/' ,'myfunctions')]", name);
            var query = backend.Index(Arm.Properties)?.Index(Arm.Credentials)?.Index(Arm.Query);
            Assert.IsNotNull(query);
            var codes = query.ValueWithType<JArray>(Arm.Code);
            Assert.AreEqual(1, codes.Count);
            Assert.AreEqual("{{myfunctions-key}}", codes[0].Value<string>());
            Assert.AreEqual("[concat('https://',toLower(parameters('myfunctions_siteName')),'.azurewebsites.net/api')]",
                backend["properties"]?.Value(Arm.Url));
        }

        [TestMethod]
        public void TestServiceContainsBackendWith2DependsOn()
        {
            var backend = GetResourceFromTemplate(ResourceType.Backend, false, true);
            var dependsOn = backend.DependsOn();

            Assert.AreEqual(2, dependsOn.Count());
            Assert.IsTrue(dependsOn.Contains(
                "[resourceId('Microsoft.ApiManagement/service/properties', parameters('service_ibizmalo_name'),'myfunctions-key')]"));
        }

        [TestMethod]
        public void TestServiceContainsOpenIdConnectProvider()
        {
            var openIdConnectProvider = GetResourceFromTemplate(ResourceType.OpenIdConnectProvider, false);
            Assert.IsNotNull(openIdConnectProvider);

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', 'myOpenIdProvider')]",
                openIdConnectProvider.Value(Arm.Name));
        }

        [TestMethod]
        public void TestServiceContainsApplicationInsightsInstanceWhenCreateApplicationInsightsInstanceIsTrue()
        {
            var appInsightsInstance =
                GetSubResourceFromTemplate(ResourceType.Service, ResourceType.ApplicationInsights, true).First();

            Assert.IsNotNull(appInsightsInstance);

            Assert.AreEqual("[parameters('service_ibizmalo_applicationInsights')]",
                appInsightsInstance.Value(Arm.Name));
            Assert.AreEqual("[parameters('service_ibizmalo_location')]", appInsightsInstance.Value(Arm.Location));
            Assert.AreEqual("other", appInsightsInstance.Value(Arm.Kind));
            Assert.AreEqual("other", appInsightsInstance.Index(Arm.Properties)?.Value(Arm.ApplicationType));
            var dependsOn = appInsightsInstance.DependsOn();
            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service', parameters('service_ibizmalo_name'))]",
                dependsOn.SingleOrDefault());
            Assert.AreEqual("2015-05-01", appInsightsInstance.Value(Arm.ApiVersion));

        }

        [TestMethod]
        public void TestServiceContainsOpenIdConnectProviderWithParametrizePropertiesOnlyAsFalse()
        {
            var openIdConnectProvider = GetResourceFromTemplate(ResourceType.OpenIdConnectProvider, false, false);
            Assert.IsNotNull(openIdConnectProvider);

            Assert.AreEqual(
                "[concat(parameters('service_ibizmalo_name'), '/', parameters('openidConnectProvider_myOpenIdProvider_name'))]",
                openIdConnectProvider.Value(Arm.Name));

            var properties = openIdConnectProvider.Index(Arm.Properties);
            Assert.IsNotNull(properties);
            Assert.AreEqual("[parameters('openidConnectProvider_myOpenIdProvider_displayname')]",
                properties.Value(Arm.DisplayName));
            Assert.AreEqual("[parameters('openidConnectProvider_myOpenIdProvider_metadataEndpoint')]",
                properties.Value(Arm.MetadataEndpoint));
            Assert.AreEqual("[parameters('openidConnectProvider_myOpenIdProvider_clientId')]",
                properties.Value(Arm.ClientId));
            Assert.AreEqual("[parameters('openidConnectProvider_myOpenIdProvider_clientSecret')]",
                properties.Value(Arm.ClientSecret));
        }

        [TestMethod]
        public void TestServiceContainsIdentityProvider()
        {
            var openIdConnectProvider = GetSubResourceFromTemplate(ResourceType.Service, ResourceType.IdentityProvider)
                .FirstOrDefault();
            Assert.IsNotNull(openIdConnectProvider);

            Assert.AreEqual("microsoft", openIdConnectProvider.Index(Arm.Properties).Value(Arm.Type));
            Assert.AreEqual("[parameters('identityProvider_Microsoft_clientId')]",
                openIdConnectProvider.Index(Arm.Properties).Value(Arm.ClientId));
            Assert.AreEqual("[parameters('identityProvider_Microsoft_clientSecret')]",
                openIdConnectProvider.Index(Arm.Properties).Value(Arm.ClientSecret));
        }

        [TestMethod]
        public void TestServiceContainsDiagnosticsForApplicationInsightsWhenCreateApplicationInsightsInstanceIsFalse()
        {
            AssertDiagnostic(false, true, "'appInsights'", "'service_ibizmalo_applicationInsights'");
        }

        [TestMethod]
        public void
            TestServiceContainsDiagnosticsForApplicationInsightsWhenCreateApplicationInsightsInstanceIsFalseAndParameterizePropertiesOnlyIsFalse()
        {
            AssertDiagnostic(false, false, "parameters('diagnostic_appInsights_name')",
                "parameters('logger_appInsights_name')");
        }

        [TestMethod]
        public void TestServiceContainsDiagnosticsForApplicationInsightsWhenCreateApplicationInsightsInstanceIsTrue()
        {
            AssertDiagnostic(true, true, "'appInsights'", "parameters('service_ibizmalo_applicationInsights')");
        }

        [TestMethod]
        public void TestServiceContainsDiagnosticsForAzureMonitor()
        {
            var diagnostic = GetSubResourceFromTemplate(ResourceType.Service, ResourceType.Diagnostic).Skip(1)
                .SingleOrDefault();
            Assert.IsNotNull(diagnostic);
            Assert.AreEqual($"[concat(parameters('service_ibizmalo_name'), '/', 'azuremonitor')]",
                diagnostic.Value(Arm.Name));
            Assert.AreEqual($"[parameters('diagnostic_azuremonitor_alwaysLog')]",
                diagnostic["properties"].Value(Arm.AlwaysLog));
            Assert.AreEqual($"[parameters('diagnostic_azuremonitor_samplingPercentage')]",
                diagnostic.Index(Arm.Properties).Index(Arm.Sampling).Value(Arm.Percentage));
        }


        private void AssertDiagnostic(bool createApplicationInsightsInstance, bool parametrizePropertiesOnly,
            string name, string loggerName)
        {
            var diagnostics = GetSubResourceFromTemplate(ResourceType.Service, ResourceType.Diagnostic,
                    createApplicationInsightsInstance, parametrizePropertiesOnly)
                .FirstOrDefault();
            Assert.IsNotNull(diagnostics);
            Assert.AreEqual("[parameters('diagnostic_appInsights_alwaysLog')]",
                diagnostics.Index(Arm.Properties).Value(Arm.AlwaysLog));
            Assert.AreEqual($"[concat(parameters('service_ibizmalo_name'), '/', {name})]",
                diagnostics.Value(Arm.Name));
            Assert.AreEqual($"2018-06-01-preview", diagnostics.Value(Arm.ApiVersion));
            var loggerResource =
                $"[resourceId('Microsoft.ApiManagement/service/loggers', parameters('service_ibizmalo_name'), parameters('service_ibizmalo_applicationInsights'))]";
            Assert.AreEqual(loggerResource, diagnostics.Index(Arm.Properties).Value(Arm.LoggerId));
            var dependsOn = diagnostics.DependsOn();
            Assert.AreEqual(2, dependsOn.Count());
            Assert.AreEqual(loggerResource, dependsOn.Last());
        }

        [TestMethod]
        public void TestContainsParametersForIdentityProvider()
        {
            var template = GetTemplate(true, false);
            AssertParameter(template, "identityProvider_Microsoft_clientId", "08de6f9f-6ac8-4b4d-ab31-d2234a3e5557",
                "string");
            AssertParameter(template, "identityProvider_Microsoft_clientSecret", String.Empty, "securestring");
        }

        [TestMethod]
        public void TestContainsParametersForOpenIdConnectProvider()
        {
            var template = GetTemplate(true, false);
            AssertParameter(template, "logger_bpst-apim-l-6a12e_connectionString", String.Empty, "securestring");
            AssertParameter(template, "logger_bpst-apim-l-6a12e_credentialName", "bpst-apim-eh-234ad", "string");
        }

        [TestMethod]
        public void TestServiceContainsParametersForApplicationInsightsDiagnostic()
        {
            var template = GetTemplate(true, false);
            AssertParameter(template, "diagnostic_appInsights_samplingPercentage", "100", "string");
            AssertParameter(template, "diagnostic_appInsights_alwaysLog", "allErrors", "string");
            AssertParameter(template, "diagnostic_appInsights_enableHttpCorrelationHeaders", "True", "string");
        }

        [TestMethod]
        public void TestServiceContainsParametersVirtualNetwork()
        {
            var template = GetTemplate(true, false);
            AssertParameter(template, "service_ibizmalo_virtualNetworkType", "External", "string");
            AssertParameter(template, "service_ibizmalo_virtualNetwork_subnetResourceId",
                "/subscriptions/c107df29-a4af-4bc9-a733-f88f0eaa4296/resourceGroups/PreDemoTest/providers/Microsoft.Network/virtualNetworks/Network/subnets/default",
                "string");
            AssertParameter(template, "service_ibizmalo_virtualNetwork_vnetid", "00000000-0000-0000-0000-000000000000",
                "string");
            AssertParameter(template, "service_ibizmalo_virtualNetwork_subnetname", "", "string");
        }

        [TestMethod]
        public void TestServiceContainsParametersForAzureMonitorDiagnostic()
        {
            var template = GetTemplate(true, false);
            AssertParameter(template, "diagnostic_azuremonitor_samplingPercentage", "100", "string");
            AssertParameter(template, "diagnostic_azuremonitor_alwaysLog", "", "string");
        }


        [TestMethod]
        public void TestContainsParametersForApplicationInsightsServiceName()
        {
            var template = GetTemplate(true, false);
            AssertParameter(template, "service_ibizmalo_applicationInsights", "appInsights", "string");
        }

        [TestMethod]
        public void TestAPIDoesNotContainOperationsWhenExportSwaggerDefinitionIsTrue()
        {
            var operations = GetSubResourceFromTemplate(ResourceType.Api, ResourceType.Operation, exportSwaggerDefinition:true);
            Assert.AreEqual(0, operations.Count());
        }

        [TestMethod]
        public void TestAPIContainSwaggerFileWhenExportSwaggerDefinitionIsTrue()
        {
            var api = GetResourceFromTemplate(ResourceType.Api, exportSwaggerDefinition:true);
            Assert.IsNotNull(api);
            var properties = api.Index(Arm.Properties);
            Assert.AreEqual("swagger-json", properties.Value(Arm.ContentFormat));
            var content = properties?.Value(Arm.ContentValue);
            Assert.IsNotNull(content);
            Assert.IsTrue(content.Contains("\"swagger\": \"2.0\","));
        }

        [TestMethod]
        public void TestAPIContainSwaggerWithCorrectHostFileWhenExportSwaggerDefinitionIsTrue()
        {
            var api = GetResourceFromTemplate(ResourceType.Api, exportSwaggerDefinition:true);
            Assert.IsNotNull(api);
            var json =api.Index(Arm.Properties)?.Index(Arm.ContentValue).Value<string>();
            var jobject = JObject.Parse(json);
            Assert.AreEqual("ibizmalotest-backend.azure-api.net", jobject.Value(Arm.Host));
            Assert.AreEqual("/tfsBackend", jobject.Value(Arm.BasePath));
            var schemes = jobject.ValueWithType<JArray>(Arm.Schemes);
            Assert.AreEqual(1, schemes.Count);
            Assert.AreEqual("http", schemes.First().Value<string>());
        }

        [TestMethod]
        public void TestAPIDoesNotContainSchemasWhenExportSwaggerDefinitionIsTrue()
        {
            var policies = GetSubResourceFromTemplate(ResourceType.Api, ResourceType.OperationPolicy, exportSwaggerDefinition:true);
            Assert.AreNotEqual(0, policies.Count());
        }

        [TestMethod]
        public void TestContainsParametersForAzureEventHubLogger()
        {
            var template = GetTemplate(true, false);
            AssertParameter(template, "openidConnectProvider_myOpenIdProvider_name", "myOpenIdProvider", "string");
            AssertParameter(template, "openidConnectProvider_myOpenIdProvider_displayname", "My OpenId Provider", "string");
            AssertParameter(template, "openidConnectProvider_myOpenIdProvider_metadataEndpoint", "https://login.microsoftonline.com/ibiz.emea.microsoftonline.com/.well-known/openid-configuration", "string");
            AssertParameter(template, "openidConnectProvider_myOpenIdProvider_clientId", "a979b408-e0e3-492d-bef4-84756d0adf92", "string");
            AssertParameter(template, "openidConnectProvider_myOpenIdProvider_clientSecret", String.Empty, "securestring");
        }

        private static void AssertParameter(JObject template, string name, string defaultValue, string type)
        {
            var parameter = template.Index(Arm.Parameters)[name];
            Assert.IsNotNull(parameter);
            Assert.AreEqual(type, parameter.Value(Arm.Type));
            Assert.AreEqual(defaultValue, parameter.Value(Arm.DefaultValue));
        }
    }
}
