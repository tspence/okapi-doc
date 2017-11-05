using System.Collections.Generic;

namespace OkapiDoc.Models
{
    public class OkapiMethodInfo
    {
        public string Name { get; set; }
        public string URI { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string ResponseType { get; set; }
        public string ResponseTypeName { get; set; }
        public string HttpVerb { get; set; }
        public OkapiParameterInfo BodyParam { get; set; }
        public List<OkapiParameterInfo> Params { get; set; }
    }
}
