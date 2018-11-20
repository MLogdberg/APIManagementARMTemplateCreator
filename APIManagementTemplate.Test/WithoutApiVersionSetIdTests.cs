using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    //TODO: Add support for Diagnostics (on service and api)

    [TestClass]
    public class WithoutApiVersionSetIdTests
    {
        private const string ProductPolicyResourceType = "Microsoft.ApiManagement/service/products/policies";
        private const string ServicePolicyResourceType = "Microsoft.ApiManagement/service/policies";
        private const string ApiResourceType = "Microsoft.ApiManagement/service/apis";
        private const string ProductResourceType = "Microsoft.ApiManagement/service/apis/products";
        private const string ServiceResourceType = "Microsoft.ApiManagement/service";
        private const string LoggerResourceType = "Microsoft.ApiManagement/service/loggers";
        private const string SchemaResourceType = "Microsoft.ApiManagement/service/apis/schemas";
        private const string BackendResourceType = "Microsoft.ApiManagement/service/backends";
        private const string OpenIdConnectProviderResourceType = "Microsoft.ApiManagement/service/openidConnectProviders";
        private const string CertificateResourceType = "Microsoft.ApiManagement/service/certificates";
        private const string OperationResourceType = "Microsoft.ApiManagement/service/apis/operations";
        private const string OperationPolicyResourceType = "Microsoft.ApiManagement/service/apis/operations/policies";
        private const string ApiPolicyResourceType = "Microsoft.ApiManagement/service/apis/policies";

        private IResourceCollector collector;

        [TestInitialize()]
        public void Initialize()
        {
            this.collector = new MockResourceCollector("WithoutApiVersionSetId");

        }

        private JObject _template = null;

        private JObject GetTemplate(bool exportProducts = false, bool parametrizePropertiesOnly = true, bool replaceSetBackendServiceBaseUrlAsProperty = false, bool fixedServiceNameParameter = false)
        {
            if (this._template != null)
                return this._template;
            var generator = new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest",
                "maloapimtestclean", false, exportProducts, true, parametrizePropertiesOnly, this.collector, replaceSetBackendServiceBaseUrlAsProperty, fixedServiceNameParameter);
            this._template = generator.GenerateTemplate().GetAwaiter().GetResult();
            return this._template;
        }


        private JToken GetResourceFromTemplate(string resourceType, bool parametrizePropertiesOnly = true, bool fixedServiceNameParameter = false)
        {
            var template = GetTemplate(true, parametrizePropertiesOnly, fixedServiceNameParameter: fixedServiceNameParameter);
            return template["resources"].FirstOrDefault(rr => rr["type"].Value<string>() == resourceType);
        }

        [TestMethod]
        public void TestApiVersionSetIdForApiIsNotSet()
        {
            JToken api = GetResourceFromTemplate(ApiResourceType);
            Assert.IsNotNull(api);

            var versionSetId = api["properties"]["apiVersionSetId"];
            Assert.IsNull(versionSetId);
        }



        [TestMethod]
        public void TestProductContainsPolicy()
        {
            IEnumerable<JToken> policies = GetSubResourceFromTemplate(ProductResourceType, ProductPolicyResourceType);

            Assert.AreEqual(1, policies.Count());
        }


        [TestMethod]
        public void TestContainsCertificate()
        {
            JToken certificate = GetResourceFromTemplate(CertificateResourceType, true);
            Assert.IsNotNull(certificate);

            var properties = certificate["properties"];
            Assert.IsNotNull(properties);

            var data = properties["data"];
            Assert.IsNotNull(data);
            Assert.AreEqual("[parameters('certificate_MyCertificate_data')]", data.Value<string>());

            var password = properties["password"];
            Assert.IsNotNull(password);
            Assert.AreEqual("[parameters('certificate_MyCertificate_password')]", password.Value<string>());

            Assert.AreEqual(2, properties.Children().Count());
        }

        [TestMethod]
        public void TestServiceNameParameterIsApimServiceNameWhenUseFixedServiceNameParameter()
        {
            var service = GetResourceFromTemplate(ServiceResourceType, fixedServiceNameParameter: true);
            Assert.AreEqual("[parameters('apimServiceName')]", service.Value<string>("name"));
        }

        [TestMethod]
        public void TestServiceContainsAppInsightsLogger()
        {
            IEnumerable<JToken> loggers = GetSubResourceFromTemplate(ServiceResourceType, LoggerResourceType);

            Assert.AreEqual(2, loggers.Count());

            var logger = loggers.First(x => x.Value<string>("name").Contains("appInsights"));
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'),'/','appInsights')]",
                logger.Value<string>("name"));
            Assert.AreEqual("Microsoft.ApiManagement/service/loggers", logger.Value<string>("type"));
            var properties = logger["properties"];
            Assert.IsNotNull(properties);
            Assert.AreEqual("applicationInsights", properties.Value<string>("loggerType"));

            var credentials = properties["credentials"];
            Assert.IsNotNull(credentials);

            Assert.AreEqual("{{Logger-Credentials-5b5dbaa35a635f22ac9db432}}",
                credentials.Value<string>("instrumentationKey"));
        }
        [TestMethod]
        public void TestServiceContainsAzureEventhubLogger()
        {
            IEnumerable<JToken> loggers = GetSubResourceFromTemplate(ServiceResourceType, LoggerResourceType);

            Assert.AreEqual(2, loggers.Count());

            var logger = loggers.First(x => x.Value<string>("name").Contains("bpst-apim-l-6a12e"));
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'),'/','bpst-apim-l-6a12e')]",
                logger.Value<string>("name"));
            Assert.AreEqual("Microsoft.ApiManagement/service/loggers", logger.Value<string>("type"));
            var properties = logger["properties"];
            Assert.IsNotNull(properties);
            Assert.AreEqual("azureEventHub", properties.Value<string>("loggerType"));

            var credentials = properties["credentials"];
            Assert.IsNotNull(credentials);

            Assert.AreEqual("[parameters('logger_bpst-apim-l-6a12e_credentialName')]", credentials.Value<string>("name"));
            Assert.AreEqual("[parameters('logger_bpst-apim-l-6a12e_connectionString')]", credentials.Value<string>("connectionString"));
        }

        private IEnumerable<JToken> GetSubResourceFromTemplate(string resourceType, string subResourceType)
        {
            JToken resource = GetResourceFromTemplate(resourceType);
            return resource.Value<JArray>("resources").Where(x => x.Value<string>("type") == subResourceType);
        }

        [TestMethod]
        public void TestProductContainsPolicyWithCorrectName()
        {
            var policy = GetSubResourceFromTemplate(ProductResourceType, ProductPolicyResourceType).First();

            var name = policy.Value<string>("name");
            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/', 'unlimited', '/', 'policy')]", name);
        }

        [TestMethod]
        public void TestApiContainsPolicyReplacedSetBaseUrl()
        {
            var template = GetTemplate();
            var apiPolicies = template.SelectTokens($"$..resources[?(@.type=='{ApiPolicyResourceType}')]");
            var policy = apiPolicies.FirstOrDefault();
            Assert.IsNotNull(policy);
            var policyContent = policy["properties"]?.Value<string>("policyContent");

            Assert.IsTrue(policyContent.StartsWith("[Concat('"));

        }

        [TestMethod]
        public void TestApiContainsPolicyReplacedSetBaseUrlAsPropertyWhenReplaceSetBaseUrlAsPropertyIsTrue()
        {
            var template = GetTemplate(replaceSetBackendServiceBaseUrlAsProperty: true);
            var apiPolicies = template.SelectTokens($"$..resources[?(@.type=='{ApiPolicyResourceType}')]");
            var policy = apiPolicies.FirstOrDefault();
            Assert.IsNotNull(policy);
            var policyContent = policy["properties"]?.Value<string>("policyContent");

            Assert.IsFalse(policyContent.StartsWith("[Concat('"));
            Assert.IsTrue(policyContent.Contains("{{api_tfs_backendurl}}"));

        }

        [TestMethod]
        public void TestApiContainsPropertyWhenReplaceSetBaseUrlAsPropertyIsTrue()
        {
            var template = GetTemplate(replaceSetBackendServiceBaseUrlAsProperty: true);
            var property = template.SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/properties')]")
                .SingleOrDefault(x => x.Value<string>("name").Contains("api_tfs_backendurl"));
            Assert.IsNotNull(property);
        }

        [TestMethod]
        public void TestProductContainsPolicyThatDependsOnProduct()
        {
            var policy = GetSubResourceFromTemplate(ProductResourceType, ProductPolicyResourceType).First();

            var dependsOn = policy.Value<JArray>("dependsOn");
            Assert.IsTrue(dependsOn.Any(x =>
                x.Value<string>() ==
                "[resourceId('Microsoft.ApiManagement/service/products', parameters('service_ibizmalo_name'), 'unlimited')]"));
        }

        [TestMethod]
        public void TestServiceContainsPolicyWithCorrectName()
        {
            var policy = GetSubResourceFromTemplate(ServiceResourceType, ServicePolicyResourceType).First();

            Assert.IsNotNull(policy);
            var name = policy.Value<string>("name");
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
            var property = template.SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/properties')]")
                .SingleOrDefault(x => x.Value<string>("name").Contains("5b5dbaa35a635f22ac9db431"));

            Assert.IsNotNull(property);
            var dependsOn = property.Value<JArray>("dependsOn");
            var hasServiceDependency = dependsOn.Any(x => x.Value<string>() ==
                                                          "[resourceId('Microsoft.ApiManagement/service', parameters('service_ibizmalo_name'))]");
            Assert.IsTrue(hasServiceDependency);
        }

        [TestMethod]
        public void TestServiceContainsPropertyForBackend()
        {
            var template = GetTemplate();
            var property = template.SelectTokens("$.resources[?(@.type=='Microsoft.ApiManagement/service/properties')]")
                .SingleOrDefault(x => x.Value<string>("name").Contains("myfunctions-key"));

            Assert.IsNotNull(property);
            var dependsOn = property.Value<JArray>("dependsOn");
            var hasServiceDependency = dependsOn.Any(x => x.Value<string>() ==
                                                          "[resourceId('Microsoft.ApiManagement/service', parameters('service_ibizmalo_name'))]");
            Assert.IsTrue(hasServiceDependency);
        }

        [TestMethod]
        public void TestServiceContainsPolicyWithCorrectDependsOn()
        {
            var policy = GetSubResourceFromTemplate(ServiceResourceType, ServicePolicyResourceType).First();

            Assert.IsNotNull(policy);
            var dependsOn = policy.Value<JArray>("dependsOn");
            Assert.AreEqual(1, dependsOn.Count());
            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service', parameters('service_ibizmalo_name'))]",
                dependsOn[0]);
        }

        [TestMethod]
        public void TestServiceContainsSchema()
        {
            var schema = GetSubResourceFromTemplate(ApiResourceType, SchemaResourceType).First();
            var name = schema.Value<string>("name");

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'),'/','tfs','/','5b7572981142a50298c7f4a6')]",
                name);
        }

        [TestMethod]
        public void TestServiceContainsBackend()
        {
            var backend = GetResourceFromTemplate(BackendResourceType, true);
            var name = backend.Value<string>("name");

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'), '/' ,'myfunctions')]", name);
            var query = backend["properties"]?["credentials"]?["query"];
            Assert.IsNotNull(query);
            var codes = query.Value<JArray>("code");
            Assert.AreEqual(1, codes.Count);
            Assert.AreEqual("{{myfunctions-key}}", codes[0].Value<string>());
        }

        [TestMethod]
        public void TestServiceContainsOpenIdConnectProvider()
        {
            var openIdConnectProvider = GetResourceFromTemplate(OpenIdConnectProviderResourceType);
            Assert.IsNotNull(openIdConnectProvider);

            Assert.AreEqual("[concat(parameters('service_ibizmalo_name'),'/','myOpenIdProvider')]",
                openIdConnectProvider.Value<string>("name"));
        }

        [TestMethod]
        public void TestServiceContainsOpenIdConnectProviderWithParametrizePropertiesOnlyAsFalse()
        {
            var openIdConnectProvider = GetResourceFromTemplate(OpenIdConnectProviderResourceType, false);
            Assert.IsNotNull(openIdConnectProvider);

            Assert.AreEqual(
                "[concat(parameters('service_ibizmalo_name'),'/',parameters('openidConnectProvider_myOpenIdProvider_name'))]",
                openIdConnectProvider.Value<string>("name"));

            var properties = openIdConnectProvider["properties"];
            Assert.IsNotNull(properties);
            Assert.AreEqual("[parameters('openidConnectProvider_myOpenIdProvider_displayname')]",
                properties.Value<string>("displayName"));
            Assert.AreEqual("[parameters('openidConnectProvider_myOpenIdProvider_metadataEndpoint')]",
                properties.Value<string>("metadataEndpoint"));
            Assert.AreEqual("[parameters('openidConnectProvider_myOpenIdProvider_clientId')]",
                properties.Value<string>("clientId"));
            Assert.AreEqual("[parameters('openidConnectProvider_myOpenIdProvider_clientSecret')]",
                properties.Value<string>("clientSecret"));
        }

        [TestMethod]
        public void TestContainsParametersForOpenIdConnectProvider()
        {
            var template = GetTemplate(true, false);
            AssertParameter(template, "logger_bpst-apim-l-6a12e_connectionString", String.Empty, "securestring");
            AssertParameter(template, "logger_bpst-apim-l-6a12e_credentialName", "bpst-apim-eh-234ad", "string");
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
            var parameter = template["parameters"][name];
            Assert.IsNotNull(parameter);
            Assert.AreEqual(type, parameter.Value<string>("type"));
            Assert.AreEqual(defaultValue, parameter.Value<string>("defaultValue"));
        }
    }
}
