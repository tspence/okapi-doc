using System.Collections.Generic;

namespace OkapiDoc.Models
{
    public class OkapiInfo
    {
        public List<OkapiMethodInfo> Methods { get; set; }
        public List<OkapiEnumInfo> Enums { get; set; }
        public List<OkapiModelInfo> Models { get; set; }
        public List<string> Categories { get; set; }

        public string ApiVersion { get; set; }
        public string ApiVersionPeriodsOnly { get; set; }
        public string ApiVersionThreeSegmentsOnly { get; set; }

        public OkapiInfo()
        {
            Methods = new List<OkapiMethodInfo>();
            Enums = new List<OkapiEnumInfo>();
            Models = new List<OkapiModelInfo>();
        }
    }
}
