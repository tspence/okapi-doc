using Newtonsoft.Json;
using System.Collections.Generic;

namespace OkapiDoc.Swagger
{
    public class SwaggerMethod
    {
        public List<string> tags { get; set; }
        public string summary { get; set; }
        public string description { get; set; }
        public string operationId { get; set; }
        public List<string> consumes { get; set; }
        public List<string> produces { get; set; }
        public List<SwaggerProperty> parameters { get; set; }
        public Dictionary<string, SwaggerResult> responses { get; set; }
        public bool deprecated { get; set; }
        public List<Dictionary<string, object>> security { get; set; }

        [JsonExtensionData]
        public IDictionary<string, object> Extended { get; set; }
    }
}
