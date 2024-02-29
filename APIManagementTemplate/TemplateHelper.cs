using System.Xml.Linq;

namespace APIManagementTemplate;

public class TemplateHelper
{
    public static string GetBackendIdFromnPolicy(string policyContent)
    {
        var docu = XDocument.Parse(policyContent);
        var backend = docu.Descendants("set-backend-service").Where(a => a.Attribute("backend-id") != null && a.Attribute("backend-id").Value != "apim-generated-policy").LastOrDefault();
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

    public static string GetAPIMGenereatedRewritePolicyTemplate(string policyContent)
    {
        var docu = XDocument.Parse(policyContent);
        var rewritePolicy = docu.Descendants("rewrite-uri").LastOrDefault(dd => dd.HasAttributes 
            && dd.Attribute(XName.Get("id"))?.Value.Equals("apim-generated-policy", StringComparison.CurrentCultureIgnoreCase) == true);

        var id = rewritePolicy?.Attribute("template");
        return id == null ? "" : id.Value;
    }
}