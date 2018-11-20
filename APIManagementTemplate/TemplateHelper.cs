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
            var backend = docu.Descendants("set-backend-service").LastOrDefault();
            if (backend != null && backend.Attribute("backend-id") != null)
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
        public static string GetCertificateThumbPrintIdFromPolicy(string policyContent)
        {
            try
            {
                var docu = XDocument.Parse(policyContent);
                var backend = docu.Descendants("authentication-certificate").LastOrDefault();
                if (backend != null && backend.Attribute("thumbprint") != null)
                {
                    XAttribute thumbprint = backend.Attribute("thumbprint");
                    if (thumbprint == null)
                    {
                        return "";
                    }
                    return thumbprint.Value;
                }
            }
            catch (Exception e)
            {
            }
            return "";
        }
    }
}
