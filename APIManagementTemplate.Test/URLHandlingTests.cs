using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using APIManagementTemplate.Models;
using System.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class URLHandlingTests
    {
        private IResourceCollector collector;
        [TestInitialize()]
        public void Initialize()
        {            
            this.collector = new MockResourceCollector("UrlHandling");
            
        }

        private TemplateGenerator GetTemplateGenerator()
        {
            return new TemplateGenerator("apidev", "1fake145-d7fa-4d0f-b406-7394a2b64fb4", "Api-Default-West-Europe", "api/documents/invoices", false, false, false, false, this.collector);
        }



        [TestMethod]
        public void LoadTemplate()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            Assert.IsNotNull(template);
            
        }
        
        [TestMethod]
        public void TestParameters()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = template["parameters"];
            Assert.AreEqual("apidev", obj["service_apidev_name"].Value<string>("defaultValue"));
            Assert.AreEqual("invoice-retrieval-api", obj["api_invoice-retrieval-api_name"].Value<string>("defaultValue"));
            Assert.AreEqual("1", obj["invoice-retrieval-api_apiRevision"].Value<string>("defaultValue"));
            Assert.AreEqual("https://apidev.azure-api.net/", obj["invoice-retrieval-api_serviceUrl"].Value<string>("defaultValue"));
            Assert.AreEqual("v1", obj["invoice-retrieval-api_apiVersion"].Value<string>("defaultValue"));
            Assert.AreEqual(true, obj["invoice-retrieval-api_isCurrent"].Value<bool>("defaultValue"));
            Assert.AreEqual("https://i-dev.azurewebsites.net/api/Base64ToStream", obj["api_invoice-retrieval-api_get-invoice_backendurl"].Value<string>("defaultValue"));
            Assert.AreEqual("secretvalue", obj["int0001functionkey_value"].Value<string>("defaultValue"));            
        }
        [TestMethod]
        public void TestResourcesCount()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = (JArray)template["resources"];
            Assert.AreEqual(3, obj.Count);
        }

        [TestMethod]
        public void TestAPI()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var obj = ((JArray)template["resources"]).Where( rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis").First();


            Assert.AreEqual("Microsoft.ApiManagement/service/apis", obj.Value<string>("type"));
            Assert.AreEqual("2019-01-01", obj.Value<string>("apiVersion"));

            Assert.AreEqual("[concat(parameters('service_apidev_name'), '/' ,parameters('api_invoice-retrieval-api_name'))]", obj.Value<string>("name"));
            Assert.AreEqual(3, obj["resources"].Count());
            Assert.AreEqual(2, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("Invoices", prop.Value<string>("displayName"));
            Assert.AreEqual("[parameters('invoice-retrieval-api_apiRevision')]", prop.Value<string>("apiRevision"));
            Assert.AreEqual("Retrieves invoices depending on market and invoice id", prop.Value<string>("description"));
            Assert.AreEqual("[parameters('invoice-retrieval-api_serviceUrl')]", prop.Value<string>("serviceUrl"));
            Assert.AreEqual("api/documents/invoices", prop.Value<string>("path"));
            Assert.AreEqual("[parameters('invoice-retrieval-api_isCurrent')]", prop.Value<string>("isCurrent"));
            Assert.AreEqual("[parameters('invoice-retrieval-api_apiVersion')]", prop.Value<string>("apiVersion"));
            Assert.AreEqual("[resourceId('Microsoft.ApiManagement/service/apiVersionSets',parameters('service_apidev_name'), '5b1fb4607e5c66b5cb2fe2e8')]", prop.Value<string>("apiVersionSetId"));
            Assert.AreEqual("https", prop.Value<JArray>("protocols").First().Value<string>());            
        }

        [TestMethod]
        public void TestAPIOperation()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var api = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis").First();

            var obj = ((JArray)api["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis/operations").First();
            Assert.AreEqual("Microsoft.ApiManagement/service/apis/operations", obj.Value<string>("type"));
            Assert.AreEqual("2019-01-01", obj.Value<string>("apiVersion"));

            Assert.AreEqual("[concat(parameters('service_apidev_name'), '/', parameters('api_invoice-retrieval-api_name'), '/', 'get-invoice')]", obj.Value<string>("name"));
            Assert.AreEqual(1, obj["resources"].Count());
            Assert.AreEqual(2, obj["dependsOn"].Count());

        }

        [TestMethod]
        public void TestAPIOperationPolicy()
        {
            TemplateGenerator generator = GetTemplateGenerator();
            var template = generator.GenerateTemplate().GetAwaiter().GetResult();
            var api = ((JArray)template["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis").First();

            var operation = ((JArray)api["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis/operations").First();

            var obj = ((JArray)operation["resources"]).Where(rr => rr.Value<string>("type") == "Microsoft.ApiManagement/service/apis/operations/policies").First();

            

            Assert.AreEqual("Microsoft.ApiManagement/service/apis/operations/policies", obj.Value<string>("type"));
            Assert.AreEqual("2019-01-01", obj.Value<string>("apiVersion"));

            Assert.AreEqual("[concat(parameters('service_apidev_name'), '/', parameters('api_invoice-retrieval-api_name'), '/', 'get-invoice', '/', 'policy')]", obj.Value<string>("name"));
            Assert.AreEqual(0, obj["resources"].Count());
            Assert.AreEqual(2, obj["dependsOn"].Count());

            var prop = obj["properties"];
            Assert.AreEqual("[Concat('<policies>\r\n  <inbound>\r\n    <base />\r\n    <set-variable name=\"MARKET\" value=\"@(context.Request.MatchedParameters[&quot;marketId&quot;])\" />\r\n    <set-variable name=\"INVOICEID\" value=\"@(context.Request.MatchedParameters[&quot;invoiceId&quot;])\" />\r\n    <set-variable name=\"subkey\" value=\"@(context.Subscription.PrimaryKey)\" />\r\n    <choose>\r\n      <when condition=\"@(context.Variables.GetValueOrDefault&lt;string&gt;(&quot;MARKET&quot;) == &quot;DE&quot;)\">\r\n        <send-request mode=\"new\" response-variable-name=\"bridgetecResponse\" timeout=\"30\" ignore-error=\"false\">\r\n          <set-url>@{\r\n                        var baseUrl = \"https://apidev.azure-api.net\";\r\n                        var url = baseUrl +\"/api/documents/invoices/bridgetec/v1/invoice/\" + context.Variables.GetValueOrDefault&lt;string&gt;(\"INVOICEID\");\r\n                        return url;\r\n                    }</set-url>\r\n          <set-method>GET</set-method>\r\n          <set-header name=\"Ocp-Apim-Subscription-Key\" exists-action=\"override\">\r\n            <value>@(context.Variables.GetValueOrDefault&lt;string&gt;(\"subkey\"))</value>\r\n          </set-header>\r\n        </send-request>\r\n        <choose>\r\n          <when condition=\"@( ((IResponse)context.Variables[&quot;bridgetecResponse&quot;]).StatusCode &lt; 400)\">\r\n            <set-backend-service base-url=\"',parameters('api_invoice-retrieval-api_get-invoice_backendurl'),'\" />\r\n            <rewrite-uri template=\"?code={{int0001functionkey}}\" copy-unmatched-params=\"false\" />\r\n            <set-method>POST</set-method>\r\n            <set-header name=\"Content-Type\" exists-action=\"override\">\r\n              <value>text/plain</value>\r\n            </set-header>\r\n            <set-body template=\"none\">@(((IResponse)context.Variables[\"bridgetecResponse\"]).Body.As&lt;string&gt;())</set-body>\r\n          </when>\r\n          <otherwise>\r\n            <return-response>\r\n              <set-status code=\"@( ((IResponse)context.Variables[&quot;bridgetecResponse&quot;]).StatusCode )\" reason=\"@(((IResponse)context.Variables[&quot;bridgetecResponse&quot;]).StatusReason)\" />\r\n              <set-header name=\"Content-Type\" exists-action=\"override\">\r\n                <value>application/json</value>\r\n              </set-header>\r\n              <set-body>@{\r\n                                return ((IResponse)context.Variables[\"bridgetecResponse\"]).Body.As&lt;string&gt;();\r\n                            }</set-body>\r\n            </return-response>\r\n          </otherwise>\r\n        </choose>\r\n      </when>\r\n      <otherwise>\r\n        <rewrite-uri template=\"/360/invoice/getinvoice?company={marketId}&amp;invoice={invoiceId}\" />\r\n      </otherwise>\r\n    </choose>\r\n  </inbound>\r\n  <backend>\r\n    <base />\r\n  </backend>\r\n  <outbound>\r\n    <base />\r\n    <choose>\r\n      <when condition=\"@(context.Variables.GetValueOrDefault&lt;string&gt;(&quot;MARKET&quot;) == &quot;DE&quot; &amp;&amp; context.Response.StatusCode &lt; 400)\">\r\n        <set-header name=\"Content-Disposition\" exists-action=\"override\">\r\n          <value>@{\r\n                        var defaultValue = \"attachment; filename=\" + context.Variables.GetValueOrDefault&lt;string&gt;(\"MARKET\") + \"_\" + context.Variables.GetValueOrDefault&lt;string&gt;(\"INVOICEID\") + \".pdf\";\r\n                        return ((IResponse)context.Variables[\"bridgetecResponse\"]).Headers.GetValueOrDefault(\"Content-Disposition\", defaultValue);\r\n                    }</value>\r\n        </set-header>\r\n        <set-header name=\"Content-Type\" exists-action=\"override\">\r\n          <value>application/pdf</value>\r\n        </set-header>\r\n      </when>\r\n      <when condition=\"@(context.Response.StatusCode &lt; 400 &amp;&amp; context.Response.StatusCode != 204)\">\r\n        <set-header name=\"Content-Type\" exists-action=\"override\">\r\n          <value>application/pdf</value>\r\n        </set-header>\r\n      </when>\r\n      <when condition=\"@(context.Response.StatusCode &gt; 399 &amp;&amp; context.Variables.GetValueOrDefault&lt;string&gt;(&quot;MARKET&quot;) != &quot;DE&quot;)\">\r\n        <set-body template=\"none\">@{\r\n                    var response = context.Response.Body.As&lt;string&gt;(); \r\n                    return \"{ ''error'' : { ''title'' : ''Error communicating with 360'', ''code'' : ''\"+ context.Response.StatusCode +\"'', ''detail'' : ''\"+ response +\"'', ''status'' : ''\"+ context.Response.StatusCode +\"''}}\";\r\n                }</set-body>\r\n      </when>\r\n    </choose>\r\n  </outbound>\r\n  <on-error>\r\n    <base />\r\n  </on-error>\r\n</policies>')]", prop.Value<string>("policyContent"));

        }

        [TestCleanup()]
        public void Cleanup()
        {

        }
    }
}
