using System.Collections.Generic;

namespace OkapiDoc.Models
{
    public class OkapiEnumInfo
    {
        public string EnumDataType { get; set; }
        public string Comment { get; set; }
        public List<OkapiEnumValue> Items { get; set; }
    }
}
