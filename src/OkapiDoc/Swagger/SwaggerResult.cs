using Newtonsoft.Json;
using System.Collections.Generic;

namespace OkapiDoc.Swagger
{
    public class SwaggerResult
    {
        public string description { get; set; }
        public SwaggerProperty schema { get; set; }

        [JsonExtensionData]
        public IDictionary<string, object> Extended { get; set; }
    }
}
