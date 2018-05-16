using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;

namespace ETH_GasOMeter
{
    static class Json
    {
        private static object _Object1 = new object();
        private static object _Object2 = new object();
        private static object _Object3 = new object();

        public static string SerializeFromObject(object obj)
        {
            lock (_Object1)
            {
                try { return JsonConvert.SerializeObject(obj, Formatting.Indented); }
                catch { }
                return string.Empty;
            }
        }

        public static JObject DeserializeFromURL(string url)
        {
            lock (_Object2)
            {
                string sJSON = string.Empty;
                JObject jObject = null;
                try
                {
                    using (var oClient = new HttpClient())
                    {
                        using (HttpResponseMessage oResponse = oClient.GetAsync(url).Result)
                        {
                            using (HttpContent oContent = oResponse.Content)
                            {
                                sJSON = oContent.ReadAsStringAsync().Result;
                            }
                        }
                    }
                    jObject = (JObject)JsonConvert.DeserializeObject(sJSON);
                }
                catch { }
                return jObject;
            }
        }

        public static T DeserializeFromURL<T>(string url)
        {
            lock (_Object3)
            {
                string sJSON = string.Empty;
                var jObject = (T)Activator.CreateInstance(typeof(T));
                try
                {
                    using (var oClient = new HttpClient())
                    {
                        using (HttpResponseMessage oResponse = oClient.GetAsync(url).Result)
                        {
                            using (HttpContent oContent = oResponse.Content)
                            {
                                sJSON = oContent.ReadAsStringAsync().Result;
                            }
                        }
                    }
                    jObject = JsonConvert.DeserializeObject<T>(sJSON);
                }
                catch { }
                return jObject;
            }
        }
    }
}
