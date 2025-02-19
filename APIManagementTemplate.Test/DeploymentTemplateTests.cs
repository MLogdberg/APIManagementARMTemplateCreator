using APIManagementTemplate.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class DeploymentTemplateTests
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        [TestMethod]
        public void TestFromString()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.StandardInstance-New.json");
            Assert.IsNotNull(document);

            var template = DeploymentTemplate.FromString(document);
            Assert.IsNotNull(template);
            Assert.IsInstanceOfType(template, typeof(DeploymentTemplate));
        }

        [TestMethod]
        public void TestToString()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.StandardInstance-New.json");
            var template = DeploymentTemplate.FromString(document);
            var actual = template.ToString();
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void TestAddAPIMInstanceResource()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.StandardInstance-New.json");
            var template = DeploymentTemplate.FromString(document);
            var actual = template.ToString();
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void TestAddVersionSet()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.VersionSet.VersionSetResource.json");
            var template = new DeploymentTemplate();
            var actual = template.AddVersionSet(JObject.Parse(document));            
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void TestSchema()
        {
            ResourceTemplate actual = GetSchema(false);
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void TestSchemaDependsOnWhenParametrizePropertiesOnlyIsFalse()
        {
            ResourceTemplate actual = GetSchema(false);
            Assert.AreEqual(1, actual.dependsOn.Count);
            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service/apis', parameters('service_dev_name'),parameters('api_vita-gatan_name'))]", actual.dependsOn[0].Value<string>());
        }

        [TestMethod]
        public void TestSchemaDependsOnWhenParametrizePropertiesOnlyIsTrue()
        {
            ResourceTemplate actual = GetSchema(true);
            Assert.AreEqual(1, actual.dependsOn.Count);
            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service/apis', parameters('service_dev_name'),'vita-gatan')]", actual.dependsOn[0].Value<string>());
        }

        private static ResourceTemplate GetSchema(bool parametrizePropertiesOnly = false)
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.Schema.simpleschema.json");
            var template = new DeploymentTemplate(parametrizePropertiesOnly);
            var actual = template.CreateAPISchema(JObject.Parse(document));
            return actual;
        }

        [TestMethod]
        public void TestPolicyAzureResourceLogicAppsUnmodified()
        {
            var document = JObject.Parse(Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.Policies.AzureResource-LogicApp-unmodified.json"));
            var array = document.Value<JArray>("value");

            var policy = (JObject)array[0];

            TemplateGenerator generator = new TemplateGenerator("ibizmalo", "c107df29-a4af-4bc9-a733-f88f0eaa4296", "PreDemoTest","",false,false,false,false,new MockResourceCollector("path"));

            var template = new DeploymentTemplate();
            template.CreatePolicy(policy);

            generator.PolicyHandeAzureResources(policy,"123",template);
            generator.PolicyHandleProperties(policy,"123",null);

            Assert.AreEqual(1, generator.identifiedProperties.Count);

        }



        [TestMethod]
        public void RemoveBuiltInGroups()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.StandardInstance-New.json");           
            var template = DeploymentTemplate.FromString(document);
            template.RemoveResources_BuiltInGroups();

            Assert.AreEqual(0, template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/groups" && rr["properties"].Value<string>("type") == "system").Count());
            Assert.IsNull(template.parameters["groups_guests_name_1"]);
        }

        /**
          {
          "comments": "Generalized from resource: '/subscriptions/c107df29-a4af-4bc9-a733-f88f0eaa4296/resourceGroups/PreDemoTest/providers/Microsoft.ApiManagement/service/ibizmalo'.",
          "type": "Microsoft.ApiManagement/service",
          "sku": {
              "name": "Developer",
              "capacity": 1
          },
          "name": "[parameters('service_ibizmalo_name')]",
          "apiVersion": "2017-03-01",
          "location": "West Europe",
          "tags": {},
          "scale": null,
          "properties": {
              "publisherEmail": "mattias.logdberg@ibiz-solutions.se",
              "publisherName": "ibiz",
              "notificationSenderEmail": "apimgmt-noreply@mail.windowsazure.com",
              "hostnameConfigurations": [],
              "additionalLocations": null,
              "virtualNetworkConfiguration": null,
              "customProperties": null,
              "virtualNetworkType": "None"
          },
          "dependsOn": []
      }, 

           */
        [TestMethod]
        public void TestAddAPIInstance()
        {
            var document = JObject.Parse( Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.malo-apiminstance.json"));
            var template = new DeploymentTemplate();
            template.AddAPIManagementInstance(document);
            var definition = JObject.FromObject(template);

            //check parameter default values
            Assert.AreEqual("ibizmalo", definition["parameters"]["service_ibizmalo_name"]["defaultValue"]);
            Assert.AreEqual("West Europe", definition["parameters"]["service_ibizmalo_location"]["defaultValue"]);
            Assert.AreEqual("mattias.logdberg@ibiz-solutions.se", definition["parameters"]["service_ibizmalo_publisherEmail"]["defaultValue"]);
            Assert.AreEqual("ibiz", definition["parameters"]["service_ibizmalo_publisherName"]["defaultValue"]);
            Assert.AreEqual("apimgmt-noreply@mail.windowsazure.com", definition["parameters"]["service_ibizmalo_notificationSenderEmail"]["defaultValue"]);
            Assert.AreEqual("Developer", definition["parameters"]["service_ibizmalo_sku_name"]["defaultValue"]);
            Assert.AreEqual("1", definition["parameters"]["service_ibizmalo_sku_capacity"]["defaultValue"]);

            //check definition 
            Assert.AreEqual("Microsoft.ApiManagement/service", definition["resources"][0]["type"]);
            Assert.AreEqual("[parameters('service_ibizmalo_name')]", definition["resources"][0]["name"]);
            Assert.AreEqual("2022-08-01", definition["resources"][0]["apiVersion"]);

            Assert.AreEqual("[parameters('service_ibizmalo_sku_name')]", definition["resources"][0]["sku"]["name"]);
            Assert.AreEqual("[parameters('service_ibizmalo_sku_capacity')]", definition["resources"][0]["sku"]["capacity"]);

        }
        [TestMethod]
        public void ParameterizeAPIs()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.StandardInstance-New.json");
            var template = DeploymentTemplate.FromString(document);
            template.ParameterizeAPIs();

            Assert.AreEqual("http://echoapi.cloudapp.net/api", template.parameters["apis_echo_api_serviceUrl"].Value<string>("defaultValue"));
            Assert.AreEqual("[parameters('apis_echo_api_serviceUrl')]", template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis" && rr.Value<string>("name") == "[parameters('apis_echo_api_name')]").First()["properties"].Value<string>("serviceUrl"));
            Assert.AreEqual("1", template.parameters["apis_echo_api_apiRevision"].Value<string>("defaultValue"));
            Assert.AreEqual("[parameters('apis_echo_api_apiRevision')]", template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis" && rr.Value<string>("name") == "[parameters('apis_echo_api_name')]").First()["properties"].Value<string>("apiRevision"));
        }

        [TestMethod]
        public void ParameterizeBackends()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.MaloInstance-Preview-Export.json");
            var template = DeploymentTemplate.FromString(document);
            template.ParameterizeBackends();

            Assert.AreEqual("http://www.webservicex.net", template.parameters["backends_soap2rest_stock_url"].Value<string>("defaultValue"));
            Assert.AreEqual("[parameters('backends_soap2rest_stock_url')]", template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/backends" && rr.Value<string>("name") == "[parameters('backends_soap2rest_stock_name')]").First()["properties"].Value<string>("url"));
        }


      /*  [TestMethod]
        public void ParameterizeAuthorizationServers()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.MaloInstance-Preview-Export.json");
            var template = DeploymentTemplate.FromString(document);
            template.ParameterizeAuthorizationServers();

            Assert.AreEqual("http://localhost", template.parameters["authorizationServers_57e38f3e0647c00f5092b5d3_clientRegistrationEndpoint"].Value<string>("defaultValue"));
            Assert.AreEqual("[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_clientRegistrationEndpoint')]", template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/authorizationServers" && rr.Value<string>("name") == "[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_name')]").First()["properties"].Value<string>("clientRegistrationEndpoint"));

            Assert.AreEqual("https://adfs.mycompany.com/adfs/ls/oauth2/authorize?resource=https://mycompany.com/appid", template.parameters["authorizationServers_57e38f3e0647c00f5092b5d3_authorizationEndpoint"].Value<string>("defaultValue"));
            Assert.AreEqual("[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_authorizationEndpoint')]", template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/authorizationServers" && rr.Value<string>("name") == "[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_name')]").First()["properties"].Value<string>("authorizationEndpoint"));

            Assert.AreEqual("https://adfs.mycompany.com/adfs/oauth2/token", template.parameters["authorizationServers_57e38f3e0647c00f5092b5d3_tokenEndpoint"].Value<string>("defaultValue"));
            Assert.AreEqual("[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_tokenEndpoint')]", template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/authorizationServers" && rr.Value<string>("name") == "[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_name')]").First()["properties"].Value<string>("tokenEndpoint"));

            Assert.AreEqual("mysecretpassword", template.parameters["authorizationServers_57e38f3e0647c00f5092b5d3_clientSecret"].Value<string>("defaultValue"));
            Assert.AreEqual("[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_clientSecret')]", template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/authorizationServers" && rr.Value<string>("name") == "[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_name')]").First()["properties"].Value<string>("clientSecret"));

            Assert.AreEqual("123", template.parameters["authorizationServers_57e38f3e0647c00f5092b5d3_clientId"].Value<string>("defaultValue"));
            Assert.AreEqual("[concat(resourceId('Microsoft.ApiManagement/service', parameters('service_ibizmalo_name')), [parameters('authorizationServers_57e38f3e0647c00f5092b5d3_clientId')])]", template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/authorizationServers" && rr.Value<string>("name") == "[parameters('authorizationServers_57e38f3e0647c00f5092b5d3_name')]").First()["properties"].Value<string>("clientId"));

        }*/

        [TestMethod]
        public void PREVIEWFixOperationsUrlTemplateParameters()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.MaloInstance-Preview-Export.json");
            var template = DeploymentTemplate.FromString(document);
            template.FixOperationsMissingUrlTemplateParameters();

            var oparation = template.resources.Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis/operations" && rr.Value<string>("name") == "[parameters('operations_58a31f7c0647c01610394009_name')]").First();

            //check the urlTemplate
            Assert.AreEqual("/road/county/{countyNo}/{other}", oparation["properties"].Value<string>("urlTemplate"));

            //check that we have 2 templateParameters
            Assert.AreEqual(2, oparation["properties"]["templateParameters"].Count());


            Assert.AreEqual("countyNo", oparation["properties"]["templateParameters"][0].Value<string>("name"));
            Assert.AreEqual("other", oparation["properties"]["templateParameters"][1].Value<string>("name"));
        }

        [TestMethod]
        public void ScenarioTestLogicAppUpdatedBackend()
        {
            var collector = new MockResourceCollector("UpdatedeLogicApp");
            var dtemplate = new DeploymentTemplate();

            var document = JObject.Parse(Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.UpdatedeLogicApp.service-cramoapidev-backends-LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV.json"));
            dtemplate.AddBackend(document,JObject.Parse("{\"properties\":{\"definition\": {\"triggers\": {\"manual\": {\"type\": \"Request\",\"kind\": \"Http\"}}}}}"));

            Assert.AreEqual("[substring(listCallbackUrl(resourceId(parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_subscriptionId'),parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_logicAppName'), 'manual'), '2017-07-01').basePath,0,add(10,indexOf(listCallbackUrl(resourceId(parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_subscriptionId'),parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_resourceGroup'), 'Microsoft.Logic/workflows/triggers', parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_logicAppName'), 'manual'), '2017-07-01').basePath,'/triggers/')))]", dtemplate.resources[0]["properties"].Value<string>("url"));
            Assert.AreEqual("[concat('https://management.azure.com/','subscriptions/',parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_subscriptionId'),'/resourceGroups/',parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_resourceGroup'),'/providers/Microsoft.Logic/workflows/',parameters('LogicApp_INT3502-PricelistErrorFileToSharePoint-DEV_logicAppName'))]", dtemplate.resources[0]["properties"].Value<string>("resourceId"));
            var result = dtemplate.ToString();

            //Assert.AreEqual("other", oparation["properties"]["templateParameters"][1].Value<string>("name"));

        }

        [TestMethod]
        public void ScenarioTestBackendCustomURLWithoutCredentials()
        {
            var collector = new MockResourceCollector("CustomURLBackends");
            var dtemplate = new DeploymentTemplate();

            var document = JObject.Parse(Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.CustomURLBackends.service-cramoapidev-backends-CustomUrlWithoutCredentials.json"));
            dtemplate.AddBackend(document, null);
            Assert.AreEqual("{}", dtemplate.parameters["Backend_CustomUrlWithoutCredentials_credentials"].Value<JObject>("defaultValue").ToString());
            Assert.AreEqual(dtemplate.resources[0]["properties"].Value<string>("credentials"), "[parameters('Backend_CustomUrlWithoutCredentials_credentials')]");
         }

        [TestMethod]
        public void ScenarioTestBackendCustomURLWithCredentials()
        {
            var collector = new MockResourceCollector("CustomURLBackends");
            var dtemplate = new DeploymentTemplate();

            var document = JObject.Parse(Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.CustomURLBackends.service-cramoapidev-backends-CustomUrlWithCredentials.json"));
            dtemplate.AddBackend(document, null);
            Assert.AreEqual("{}", dtemplate.parameters["Backend_CustomUrlWithoutCredentials_credentials"].Value<JObject>("defaultValue").ToString());
            Assert.AreEqual(dtemplate.resources[0]["properties"].Value<string>("credentials"), "[parameters('Backend_CustomUrlWithoutCredentials_credentials')]");
        }

        [TestMethod]
        public async Task TryFetchDeploymentSchemas()
        {
            var deploymentSchemaResponse = await _httpClient.GetAsync(Constants.deploymentSchema);
            Assert.IsTrue(deploymentSchemaResponse.IsSuccessStatusCode);

            var deploymenParameterSchemaResponse = await _httpClient.GetAsync(Constants.deploymenParameterSchema);
            Assert.IsTrue(deploymenParameterSchemaResponse.IsSuccessStatusCode);

            var parameterSchemaResponse = await _httpClient.GetAsync(Constants.parameterSchema);
            Assert.IsTrue(parameterSchemaResponse.IsSuccessStatusCode);
        }
    }

}
