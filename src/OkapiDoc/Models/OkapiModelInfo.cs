using System.Collections.Generic;

namespace OkapiDoc.Models
{
    public class OkapiModelInfo
    {
        public string Description { get; set; }
        public List<string> Required { get; set; }
        public string Type { get; set; }
        public object Example { get; set; }
        public string Comment { get; set; }
        public string SchemaName { get; set; }
        public List<OkapiParameterInfo> Properties { get; set; }
    }
}
