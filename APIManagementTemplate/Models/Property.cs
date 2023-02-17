using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace APIManagementTemplate.Models
{
    public class Property
    {
        public PropertyType type;
        public string name;
        public string extraInfo;
        public List<string> apis = new List<string>();
        public string operationName;
        public List<JObject> dependencies = new List<JObject>();

        public enum PropertyType
        {
            Standard,
            LogicApp,
            LogicAppRevisionGa,
            Function
        }
    }


}
