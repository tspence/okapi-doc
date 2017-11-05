using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OkapiDoc.Models;
using OkapiDoc.Swagger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using static OkapiDoc.TemplateBase;

namespace OkapiDoc
{
    class Program
    {
        /// <summary>
        /// Static entry point
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Setup 
            var app = new CommandLineApplication();
            app.Name = typeof(Program).Assembly.GetName().Name;
            app.Description = typeof(Program).Assembly
                .GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)
                .OfType<AssemblyDescriptionAttribute>()
                .FirstOrDefault()?.Description ?? "";

            // Supported options
            app.HelpOption("-?|-h|--help");
            var templateOption = app.Option("-g|--generate <file>", "Path to the generator template file", CommandOptionType.SingleValue);

            // Only do work if a template was specified
            app.OnExecute(() => 
            {
                if (templateOption.HasValue())
                {
                    if (File.Exists(templateOption.Value()))
                    {
                        GenerateTemplateFile(templateOption.Value());
                    }
                    else
                    {
                        Console.WriteLine($"Template file [{templateOption.Value()}] was not found.");
                    }
                }
                else
                {
                    app.ShowHelp();
                }

                return 0;
            });
            app.Execute(args);
        }

        private static void GenerateTemplateFile(string file)
        {
            // First parse the file
            SwaggerRenderTask task = ParseRenderTask(file);
            if (task == null) {
                return;
            }

            // Download the swagger file
            OkapiInfo api = DownloadSwaggerJson(task);
            if (api == null) {
                return;
            }

            // Render output
            Console.WriteLine($"***** Beginning render stage");
            Render(task, api);
        }

        private static OkapiInfo DownloadSwaggerJson(SwaggerRenderTask task)
        {
            string swaggerJson = null;
            OkapiInfo api = null;

            // Download the swagger JSON file from the server
            using (var client = new HttpClient())
            {
                try
                {
                    Console.WriteLine($"***** Downloading swagger JSON from {task.swaggerUri}");
                    var response = client.GetAsync(task.swaggerUri).Result;
                    swaggerJson = response.Content.ReadAsStringAsync().Result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception downloading swagger JSON file: {ex.ToString()}");
                    return null;
                }
            }

            // Parse the swagger JSON file
            try {
                Console.WriteLine($"***** Processing swagger JSON");
                api = ProcessSwagger(swaggerJson);
            } catch (Exception ex) {
                Console.WriteLine($"Exception processing swagger JSON file: {ex.ToString()}");
            }
            return api;
        }

        private static SwaggerRenderTask ParseRenderTask(string file)
        {
            SwaggerRenderTask task = null;
            try {
                Console.WriteLine($"***** Parsing generator script: {file}");
                var contents = File.ReadAllText(file);
                task = JsonConvert.DeserializeObject<SwaggerRenderTask>(contents);

                // Create all razor templates
                string baseFolder = Path.GetDirectoryName(file);
                foreach (var target in task.targets) {
                    target.ParseRazorTemplates(baseFolder);
                }
                return task;

            // If anything blew up, refuse to continue
            } catch (Exception ex) {
                Console.WriteLine($"Exception parsing render task: {ex.Message}");
                return null;
            }
        }

        #region Render targets
        public static void Render(SwaggerRenderTask task, OkapiInfo api)
        {
            // Render each target
            foreach (var target in task.targets) {
                Console.WriteLine($"***** Rendering {target.name}");
                target.Render(api);
            }

            // Done
            Console.WriteLine("***** Done");
        }
        #endregion

        #region Parse swagger
        public static OkapiInfo ProcessSwagger(string swagger)
        {
            // Read in the swagger object
            var settings = new JsonSerializerSettings();
            settings.MetadataPropertyHandling = MetadataPropertyHandling.Ignore;
            var obj = JsonConvert.DeserializeObject<Swagger.SwaggerModel>(swagger, settings);
            var api = Cleanup(obj);

            // Sort methods by category name and by name
            api.Methods = (from m in api.Methods orderby m.Category, m.Name select m).ToList();

            // Produce a distinct list of categories to simplify work
            api.Categories = (from m in api.Methods orderby m.Category select m.Category).Distinct().ToList();
            return api;
        }

        private static OkapiInfo Cleanup(SwaggerModel obj)
        {
            OkapiInfo result = new OkapiInfo();
            result.ApiVersion = obj.ApiVersion;

            // Set up alternative version numbers: This one does not permit dashes
            result.ApiVersionPeriodsOnly = result.ApiVersion.Replace("-", ".");

            // Set up alternative version numbers: This one permits only three segments
            var sb = new StringBuilder();
            int numPeriods = 0;
            foreach (char c in obj.ApiVersion) {
                if (c == '.') numPeriods++;
                if (numPeriods > 3 || c == '-') break;
                sb.Append(c);
            }
            result.ApiVersionThreeSegmentsOnly = sb.ToString();

            // Loop through all paths and spit them out to the console
            foreach (var path in (from p in obj.paths orderby p.Key select p)) {
                foreach (var verb in path.Value) {

                    // Set up our API
                    var api = new OkapiMethodInfo();
                    api.URI = path.Key;
                    api.HttpVerb = verb.Key;
                    api.Summary = verb.Value.summary;
                    api.Description = verb.Value.description;
                    api.Params = new List<OkapiParameterInfo>();
                    api.Category = verb.Value.tags.FirstOrDefault();
                    api.Name = verb.Value.operationId;

                    // Now figure out all the URL parameters
                    foreach (var parameter in verb.Value.parameters) {

                        // Construct parameter
                        var pi = ResolveType(parameter);

                        // Query String Parameters
                        if (parameter.paramIn == "query") {
                            pi.ParameterLocation = ParameterLocationType.QueryString;

                            // URL Path parameters
                        } else if (parameter.paramIn == "path") {
                            pi.ParameterLocation = ParameterLocationType.UriPath;

                            // Body parameters
                        } else if (parameter.paramIn == "body") {
                            pi.ParamName = "model";
                            pi.ParameterLocation = ParameterLocationType.RequestBody;
                            api.BodyParam = pi;
                        } else if (parameter.paramIn == "header") {
                            pi.ParameterLocation = ParameterLocationType.Header;
                        } else if (parameter.paramIn == "formData") {
                            pi.ParameterLocation = ParameterLocationType.FormData;
                        } else {
                            throw new Exception("Unrecognized parameter location: " + parameter.paramIn);
                        }
                        api.Params.Add(pi);

                        // Is this property an enum?
                        if (parameter.EnumDataType != null) {
                            ExtractEnum(result.Enums, parameter);
                        }
                    }

                    // Now figure out the response type
                    SwaggerResult ok = null;
                    if (verb.Value.responses.TryGetValue("200", out ok)) {
                        api.ResponseType = ok.schema == null ? null : ok.schema.type;
                        api.ResponseTypeName = ResolveTypeName(ok.schema);
                    } else if (verb.Value.responses.TryGetValue("201", out ok)) {
                        api.ResponseType = ok.schema == null ? null : ok.schema.type;
                        api.ResponseTypeName = ResolveTypeName(ok.schema);
                    }

                    // Ensure that body parameters are always last for consistency
                    if (api.BodyParam != null) {
                        api.Params.Remove(api.BodyParam);
                        api.Params.Add(api.BodyParam);
                    }

                    // Done with this API
                    result.Methods.Add(api);
                }
            }

            // Loop through all the schemas
            foreach (var def in obj.definitions) {
                var m = new OkapiModelInfo()
                {
                    SchemaName = def.Key,
                    Comment = def.Value.description,
                    Example = def.Value.example,
                    Description = def.Value.description,
                    Required = def.Value.required,
                    Type = def.Value.type,
                    Properties = new List<OkapiParameterInfo>()
                };
                foreach (var prop in def.Value.properties) {
                    if (!prop.Value.required && def.Value.required != null) {
                        prop.Value.required = def.Value.required.Contains(prop.Key);
                    }

                    // Construct property
                    var pi = ResolveType(prop.Value);
                    pi.ParamName = prop.Key;
                    m.Properties.Add(pi);

                    // Is this property an enum?
                    if (prop.Value.EnumDataType != null) {
                        ExtractEnum(result.Enums, prop.Value);
                    }
                }

                result.Models.Add(m);
            }

            // Here's your processed API
            return result;
        }

        private static void ExtractEnum(List<OkapiEnumInfo> enums, SwaggerProperty prop)
        {
            // Determine enum value comments and description, if any
            string xEnumDescription = null;
            Dictionary<string, string> xEnumValueComments = null;
            if (prop != null && prop.Extended != null) {
                xEnumDescription = prop.Extended["x-enum-description"] as string;
                JObject j = prop.Extended["x-enum-value-comments"] as JObject;
                if (j != null) {
                    xEnumValueComments = j.ToObject<Dictionary<string, string>>();
                }
            }

            // Load up the enum
            var enumType = (from e in enums where e.EnumDataType == prop.EnumDataType select e).FirstOrDefault();
            if (enumType == null) {
                enumType = new OkapiEnumInfo()
                {
                    EnumDataType = prop.EnumDataType,
                    Comment = xEnumDescription,
                    Items = new List<OkapiEnumValue>()
                };
                enums.Add(enumType);
            }

            // Add values if they are known
            if (prop.enumValues != null) {
                foreach (var s in prop.enumValues) {
                    if (!enumType.Items.Any(i => i.Value == s)) {

                        // Figure out the comment for the enum, if one is available
                        string comment = null;
                        if (xEnumValueComments != null) {
                            xEnumValueComments.TryGetValue(s, out comment);
                        }
                        enumType.Items.Add(new OkapiEnumValue() { Value = s, Comment = comment });
                    }
                }
            }
        }

        private static OkapiParameterInfo ResolveType(SwaggerProperty prop)
        {
            var pi = new OkapiParameterInfo()
            {
                Comment = prop.description ?? "",
                ParamName = prop.name,
                Type = prop.type,
                TypeName = ResolveTypeName(prop),
                Required = prop.required,
                ReadOnly = prop.readOnly,
                MaxLength = prop.maxLength,
                MinLength = prop.minLength,
                Example = prop.example == null ? "" : prop.example.ToString()
            };

            // Is this an array?
            if (prop.type == "array") {
                pi.IsArrayType = true;
                pi.ArrayElementType = ResolveTypeName(prop.items).Replace("?", "");
            } else if (pi.TypeName.StartsWith("List<")) {
                pi.IsArrayType = true;
                pi.ArrayElementType = pi.TypeName.Substring(5, pi.TypeName.Length - 6);
            }
            return pi;
        }

        private static string ResolveTypeName(SwaggerProperty prop)
        {
            StringBuilder typename = new StringBuilder();
            bool isValueType = false;

            // If this API produces a file download
            if (prop == null || prop.type == "file") {
                return "FileResult";
            }

            // Handle integers / int64s
            if (prop.type == "integer") {
                if (String.Equals(prop.format, "int64", StringComparison.CurrentCultureIgnoreCase)) {
                    typename.Append("Int64");
                } else if (String.Equals(prop.format, "byte", StringComparison.CurrentCultureIgnoreCase)) {
                    typename.Append("Byte");
                } else if (String.Equals(prop.format, "int16", StringComparison.CurrentCultureIgnoreCase)) {
                    typename.Append("Int16");
                } else if (prop.format == null || String.Equals(prop.format, "int32", StringComparison.CurrentCultureIgnoreCase)) {
                    typename.Append("Int32");
                } else {
                    Console.WriteLine("Unknown typename");
                }
                isValueType = true;

                // Handle decimals
            } else if (prop.type == "number") {
                typename.Append("Decimal");
                isValueType = true;

                // Handle boolean
            } else if (prop.type == "boolean") {
                typename.Append("Boolean");
                isValueType = true;

                // Handle date-times formatted as strings
            } else if (prop.format == "date-time" && prop.type == "string") {
                typename.Append("DateTime");
                isValueType = true;

                // Handle strings, and enums, which are represented as strings
            } else if (prop.type == "string") {

                // Base64 encoded bytes
                if (String.Equals(prop.format, "byte", StringComparison.CurrentCultureIgnoreCase)) {
                    typename.Append("Byte[]");
                } else if (prop.EnumDataType == null) {
                    return "String";
                } else {
                    typename.Append(prop.EnumDataType);
                    isValueType = true;
                }

                // But, if this is an array, nest it
            } else if (prop.type == "array") {
                typename.Append("List<");
                typename.Append(ResolveTypeName(prop.items).Replace("?", ""));
                typename.Append(">");

                // Is it a custom object?
            } else if (prop.schemaref != null) {
                string schema = prop.schemaref.Substring(prop.schemaref.LastIndexOf("/") + 1);
                if (schema.StartsWith("FetchResult")) {
                    schema = schema.Replace("[", "<");
                    schema = schema.Replace("]", ">");
                }
                typename.Append(schema);

                // Is this a nested swagger element?
            } else if (prop.schema != null) {
                typename.Append(ResolveTypeName(prop.schema));

                // Custom hack for objects that aren't represented correctly in swagger at the moment - still have to fix this in REST v2
            } else if (prop.description == "Default addresses for all lines in this document") {
                typename.Append("Dictionary<TransactionAddressType, AddressInfo>");

                // Custom hack for objects that aren't represented correctly in swagger at the moment - still have to fix this in REST v2
            } else if (prop.description == "Specify any differences for addresses between this line and the rest of the document") {
                typename.Append("Dictionary<TransactionAddressType, AddressInfo>");

                // All else is just a generic object
            } else if (prop.type == "object") {
                typename.Append("Dictionary<string, string>");

            // Catch severe problems or weird/unknown types
            } else {
                throw new NotImplementedException($"Type {prop.type} not implemented");
            }

            // Is this a basic value type that's not required?  Make it nullable
            if (isValueType && !prop.required) {
                typename.Append("?");
            }

            // Here's your type name
            return typename.ToString();
        }
        #endregion
    }
}
