using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate
{
    public class TemplateMerger
    {
        /// <summary>
        /// Merges the changes in the newObject into the oldObject.
        /// Please not that the contents of oldObject is changed!
        /// </summary>
        /// <param name="oldObject">The old object. The contents of this parameter is changed by this function</param>
        /// <param name="newObject">The new object.</param>
        /// <returns></returns>
        public static JObject Merge(JObject oldObject, JObject newObject)
        {
            foreach (KeyValuePair<string, JToken> pair in newObject)
            {
                if (oldObject[pair.Key] != null)
                {
                    if (pair.Value is JArray newArray && oldObject[pair.Key] is JArray oldArray)
                    {
                        foreach (var item in newArray)
                        {
                            if (item is JValue)
                            {
                                if (oldArray.All(x => !JToken.EqualityComparer.Equals(x, item)))
                                    oldArray.Add(item);
                            }
                            else if (item is JObject)
                            {
                                var oldItem = oldArray.OfType<JObject>().FirstOrDefault(x => SameIdentity(x, item, pair.Key));
                                if (oldItem == null)
                                {
                                    if (oldArray.OfType<JObject>().Any(x => JToken.EqualityComparer.Equals(x, item)))
                                        continue;
                                }
                                if (oldItem == null)
                                    oldArray.Add(item);
                                else
                                {
                                    Merge(oldItem, (JObject)item);
                                }
                            }
                        }
                    }
                    else if (pair.Value is JObject && oldObject[pair.Key] is JObject)
                    {
                        Merge((JObject)oldObject[pair.Key], (JObject)pair.Value);
                    }
                    else if(pair.Value is JValue && oldObject[pair.Key] is JValue)
                    {
                        oldObject[pair.Key] = pair.Value;
                    }
                }
                else
                    oldObject[pair.Key] = pair.Value;
            }
            return oldObject;
        }

        public static bool SameIdentity(JToken x, JToken y, string parent)
        {
            if (x == null || y == null)
                return false;
            switch (parent.ToLower())
            {
                case "resources":
                    return JTokenEqualsOnProperties(x, y, "name", "type");
                case "responses":
                    return JTokenEqualsOnProperties(x, y, "statusCode");
                case "representations":
                    return JTokenEqualsOnProperties(x, y, "contentType");
                case "templateparameters":
                    return JTokenEqualsOnProperties(x, y, "name");
                default:
                    return JToken.EqualityComparer.Equals(x, y);
            }
        }

        private static bool JTokenEqualsOnProperties(JToken x, JToken y, params string[] properties)
        {
            if (properties.Any(p => x.Value<string>(p) == null || y.Value<string>(p) == null))
                return false;
            return properties.All(p => x.Value<string>(p) == y.Value<string>(p));
        }
    }


}