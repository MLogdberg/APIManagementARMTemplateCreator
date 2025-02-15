using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIManagementTemplate
{
    public static class Constants
    {
        internal static readonly string deploymentSchema = @"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#";
        internal static readonly string deploymenParameterSchema = @"https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#";
        internal static readonly string parameterSchema = @"https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#";

        public static string AuthString = "https://login.microsoftonline.com/common"; //"https://login.windows.net/common/oauth2/authorize";
        public static string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
        public static string ResourceUrl = "https://management.core.windows.net/";
        public static string RedirectUrl = "urn:ietf:wg:oauth:2.0:oob";
    }
}
