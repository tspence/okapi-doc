using Newtonsoft.Json;
using System.Collections.Generic;

namespace OkapiDoc.Swagger
{
    public class SwaggerDefinition
    {
        public string description { get; set; }
        public List<string> required { get; set; }
        public string type { get; set; }
        public Dictionary<string, SwaggerProperty> properties { get; set; }
        public object example { get; set; }

        [JsonExtensionData]
        public IDictionary<string, object> Extended { get; set; }
    }
}
