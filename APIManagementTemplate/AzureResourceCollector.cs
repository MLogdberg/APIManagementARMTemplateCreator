using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace APIManagementTemplate
{
    public class AzureResourceCollector : IResourceCollector
    {

        public string DebugOutputFolder = "";
        public string token;


        public AzureResourceCollector()
        {

        }
        public string Login(string tenantName)
        {
            string authstring = Constants.AuthString;
            if (!string.IsNullOrEmpty(tenantName))
            {
                authstring = authstring.Replace("common", tenantName);
            }
            AuthenticationContext ac = new AuthenticationContext(authstring, true);

            var ar = ac.AcquireTokenAsync(Constants.ResourceUrl, Constants.ClientId, new Uri(Constants.RedirectUrl), new PlatformParameters(PromptBehavior.RefreshSession)).GetAwaiter().GetResult();
            token = ar.AccessToken;
            return token;
        }
        private static HttpClient client = new HttpClient() { BaseAddress = new Uri("https://management.azure.com") };

        public async Task<JObject> GetResource(string resourceId, string suffix = "", string apiversion = "2024-05-01")
        {
            string url = resourceId + $"{GetSeparatorCharacter(resourceId)}api-version={apiversion}" + (string.IsNullOrEmpty(suffix) ? "" : $"&{suffix}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new UnauthorizedAccessException(responseContent);
            }

            //Define response object
            var responseObject = JObject.Parse(responseContent);

            //When more data is available in the next page get that too
            var nextPageurl = responseObject["nextLink"]?.ToString();
            while (nextPageurl != null)
            {
                response = await client.GetAsync(nextPageurl);
                var rawResult = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
                nextPageurl = rawResult["nextLink"]?.ToString();
                responseObject.Merge(rawResult);
            }
            
            if (!string.IsNullOrEmpty(DebugOutputFolder))
            {
                var path = DebugOutputFolder + "\\" + EscapeString(resourceId.Split('/').SkipWhile((a) => { return a != "service" && a != "workflows" && a != "sites"; }).Aggregate<string>((b, c) => { return b + "-" + c; }) + ".json");
                System.IO.File.WriteAllText(path, response.ToString());
            }

            return responseObject;

        }

        private static string GetSeparatorCharacter(string resourceId)
        {
            return resourceId.Contains("?") ? "&" : "?";
        }

        public async Task<JObject> GetResourceByURL(string url)
        {
            var response = await new HttpClient().GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(DebugOutputFolder))
            {
                var uri = new Uri(url);
                var path = EscapeString(uri.AbsolutePath);
                System.IO.File.WriteAllText($"{DebugOutputFolder}\\{uri.Host}{path}", responseContent);
            }
            return JObject.Parse(responseContent);

        }

        public static string EscapeString(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return value;
            return value.Replace("/", "-").Replace(" ", "-").Replace("=", "-").Replace("&", "-").Replace("?", "-");
        }
    }
}
