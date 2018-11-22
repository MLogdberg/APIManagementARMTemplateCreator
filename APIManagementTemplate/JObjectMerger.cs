using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate
{
    public class JObjectMerger
    {
        public static JObject Merge(JObject oldObject, JObject newObject, string idPropertyName = "name")
        {
            var comparer = new JTokenComparer();
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
                                var oldItem = oldArray.OfType<JObject>().FirstOrDefault(x => JTokenComparer.SameNameAndType(x, item));
                                if (oldItem == null)
                                {
                                    if (oldArray.OfType<JObject>().Any(x => JToken.EqualityComparer.Equals(x, item)))
                                        continue;
                                }
                                if (oldItem == null)
                                    oldArray.Add(item);
                                else
                                {
                                    Merge(oldItem, (JObject)item, idPropertyName);
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
                    else
                    {
                        var sju = 7;
                    }
                }

                else
                    oldObject[pair.Key] = pair.Value;
            }
            return oldObject;
        }

        public class JTokenComparer
        {
            public static bool SameNameAndType(JToken x, JToken y)
            {
                if (x == null || y == null)
                    return false;
                var nameX = x.Value<string>("name");
                var typeX = x.Value<string>("type");
                var nameY = y.Value<string>("name");
                var typeY = y.Value<string>("type");

                if (nameX == null || typeX == null || nameY == null || typeY == null)
                    return false;
                return nameX == nameY && typeX == typeY;
            }

            public static int GetHashCode(JObject obj)
            {
                var name = obj.Value<string>("name");
                var type = obj.Value<string>("type");
                if (name != null && type != null)
                    return $"{name}:{type}".GetHashCode();
                return obj.GetHashCode();
            }
        }
    }


}