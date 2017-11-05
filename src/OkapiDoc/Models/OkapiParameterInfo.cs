﻿using static OkapiDoc.TemplateBase;

namespace OkapiDoc.Models
{
    public class OkapiParameterInfo
    {
        public string ParamName { get; set; }
        public string CleanParamName
        {
            get
            {
                return ParamName.Replace("$", "");
            }
        }

        public string StrippedPackageParamName
        {
            get
            {
                var cleanedParam = CleanParamName;
                var index = cleanedParam.LastIndexOf(".") + 1;
                return cleanedParam.Substring(index, cleanedParam.Length - index);
            }
        }
        public string Type { get; set; }
        public string TypeName { get; set; }
        public string Comment { get; set; }
        public ParameterLocationType ParameterLocation { get; set; }

        public bool IsArrayType { get; set; }
        public string ArrayElementType { get; set; }
        public bool Required { get; set; }
        public bool ReadOnly { get; set; }
        public int? MaxLength { get; set; }
        public int? MinLength { get; set; }
        public string Example { get; set; }
    }
}
