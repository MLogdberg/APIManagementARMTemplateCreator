using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Templates
{
    /// <summary>
    /// <example>
    /// {
    ///    "type": "Microsoft.ApiManagement/service/authorizationProviders/authorizations/accessPolicies",
    ///    "apiVersion": "2022-08-01",
    ///    "name": "string",
    ///    "properties": {
    ///      "objectId": "string",
    ///      "tenantId": "string"
    ///    }
    /// }</example>
    /// </summary>
    public class AccessPoliciesTemplate
    {
       
        public string type => "accessPolicies";
        public string apiVersion => "2022-08-01";
        public string name { get; set; }

        public AccessPolicyProperties properties { get; set; } = new AccessPolicyProperties();
        public JArray dependsOn { get; set; } = new JArray();
    }

    public class  AccessPolicyProperties
    {
        public string objectId { get; set; } 
        public string tenantId { get; set; }
    }

}
