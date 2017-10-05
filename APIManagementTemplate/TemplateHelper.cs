using APIManagementTemplate.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace APIManagementTemplate
{
    public class TemplateHelper
    {       
        public static string GetBackendIdFromnPolicy(string policyContent)
        {
            var docu = XDocument.Parse(policyContent);
            var backend = docu.Descendants("set-backend-service").FirstOrDefault();
            if (backend != null)
            {
                XAttribute id = backend.Attribute("backend-id");
                if (id == null || id.Value == "apim-generated-policy")
                {
                    return "";
                }
                return id.Value;
            }
            return "";
        }
    }
}
