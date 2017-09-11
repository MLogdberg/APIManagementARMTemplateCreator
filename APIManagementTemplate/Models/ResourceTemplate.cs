using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIManagementTemplate.Models
{
    public class ResourceTemplate
    {

        
        public string comments { get; set; }
        public string type { get; set; }
        public string name { get; set; }

        public string apiVersion
        {
            get
            {
                return "2017-03-01";
            }
        }
        public JObject properties { get; set; }

        public IList<JObject> resources { get; set; }

        public ResourceTemplate()
        {
            properties = new JObject();
            resources = new List<JObject>();
        }
    }
}
