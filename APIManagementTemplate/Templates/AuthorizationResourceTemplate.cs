using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Templates
{
    public class AuthorizationResourceTemplate
    {
        private List<string> names = new List<string>();

        public void AddName(string name)
        {
            names.Add(name);
        }

        public void RemoveNameAt(int i)
        {
            names.RemoveAt(i);
        }
        public string GetResourceId()
        {
            return String.Join(", ", names);
        }

        public string type { get; } = "authorizations";
        public string name
        {
            get
            {
                if (string.IsNullOrEmpty(_name))
                    return "[concat(" + String.Join(", '/', ", names) + ")]";
                return _name;
            }
            set
            {
                _name = value;
            }
        }

        private string _name = null;
       
        public string apiVersion { get; set; } = "2021-08-01";
        public AuthorizationProperties properties { get; set; }
        
        public IList<JObject> resources { get; set; }

        public JArray dependsOn { get; set; }

        public AuthorizationResourceTemplate()
        {
            dependsOn = new JArray();
            resources = new List<JObject>();
        }
    }

    public class AuthorizationProperties
    {
        public string authorizationType { get; set; } 
        public string oauth2grantType { get; set; } 
        public AuthorizationParameters parameters { get; set; } 

    }

    public class AuthorizationParameters
    {
        public string clientId { get; set; }
        public string clientSecret { get; set; }
    }
}
