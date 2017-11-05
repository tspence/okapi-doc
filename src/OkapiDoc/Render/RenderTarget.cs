using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CSharp;
using OkapiDoc.Models;
using RazorLight;
using RazorLight.Extensions;
using RazorLight.Templating.FileSystem;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace OkapiDoc.Render
{
    public class RenderTarget
    {
        /// <summary>
        /// The name of this target
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// The root folder for all files and tasks under this target
        /// </summary>
        public string rootFolder { get; set; }

        /// <summary>
        /// The list of templates to apply for this target
        /// </summary>
        public List<RenderTemplateTask> templates { get; set; }

        /// <summary>
        /// The list of fixups to apply for this target
        /// </summary>
        public List<RenderFixupTask> fixups { get; set; }

        #region Rendering
        /// <summary>
        /// Render this particular type of client library
        /// </summary>
        /// <param name="api"></param>
        /// <param name="rootPath"></param>
        public virtual void Render(OkapiInfo api)
        {
            if (templates != null) {

                // Iterate through each template
                foreach (var template in templates) {
                    Console.WriteLine($"     Rendering {name}.{template.file}...");

                    // What type of template are we looking at?
                    switch (template.type) {

                        // A single template file for the entire API
                        case TemplateType.singleFile:
                            RenderSingleFile(api, template);
                            break;

                        // A separate file for each method category in the API
                        case TemplateType.methodCategories:
                            RenderMethodCategories(api, template);
                            break;

                        // One file per category
                        case TemplateType.methods:
                            RenderMethods(api, template);
                            break;

                        // One file per model
                        case TemplateType.models:
                            RenderModels(api, template);
                            break;

                        // One file per model
                        case TemplateType.uniqueModels:
                            RenderUniqueModels(api, template);
                            break;

                        // One file per enum
                        case TemplateType.enums:
                            RenderEnums(api, template);
                            break;
                    }
                }
            }

            // Are there any fixups?
            if (fixups != null) {
                foreach (var fixup in fixups) {
                    FixupOneFile(api, fixup);
                }
            }
        }

        private void FixupOneFile(OkapiInfo api, RenderFixupTask fixup)
        {
            var fn = Path.Combine(rootFolder, fixup.file);
            Console.Write($"Executing fixup for {fn}... ");
            if (!File.Exists(fn)) {
                Console.WriteLine(" File not found!");
            } else {

                // Determine what the new string is
                var newstring = QuickStringMerge(fixup.replacement, api);

                // What encoding did they want - basically everyone SHOULD want UTF8, but ascii is possible I guess
                Encoding e = Encoding.UTF8;
                if (fixup.encoding == "ASCII") {
                    e = Encoding.ASCII;
                }

                // Execute the fixup
                ReplaceStringInFile(fn, fixup.regex, newstring, e);
                Console.WriteLine(" Done!");
            }
        }

        private void RenderEnums(OkapiInfo api, RenderTemplateTask template)
        {
            foreach (var enumDataType in api.Enums) {
                var outputPath = Path.Combine(rootFolder, QuickStringMerge(template.output, enumDataType));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var output = template.razor.ExecuteTemplate(api, null, null, enumDataType);
                File.WriteAllText(outputPath, output);
            }
        }

        private void RenderUniqueModels(OkapiInfo api, RenderTemplateTask template)
        {
            var oldModels = api.Models;
            api.Models = (from m in api.Models where !m.SchemaName.StartsWith("FetchResult") select m).ToList();
            foreach (var model in api.Models) {
                var outputPath = Path.Combine(rootFolder, QuickStringMerge(template.output, model));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var output = template.razor.ExecuteTemplate(api, null, model, null);
                File.WriteAllText(outputPath, output);
            }
            api.Models = oldModels;
        }

        private void RenderModels(OkapiInfo api, RenderTemplateTask template)
        {
            foreach (var model in api.Models) {
                var outputPath = Path.Combine(rootFolder, QuickStringMerge(template.output, model));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var output = template.razor.ExecuteTemplate(api, null, model, null);
                File.WriteAllText(outputPath, output);
            }
        }

        private void RenderMethods(OkapiInfo api, RenderTemplateTask template)
        {
            foreach (var method in api.Methods) {
                var outputPath = Path.Combine(rootFolder, QuickStringMerge(template.output, method));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var output = template.razor.ExecuteTemplate(api, method, null, null);
                File.WriteAllText(outputPath, output);
            }
        }

        private void RenderMethodCategories(OkapiInfo api, RenderTemplateTask template)
        {
            var categories = (from m in api.Methods select m.Category).Distinct();
            foreach (var c in categories) {
                var oldMethods = api.Methods;
                api.Methods = (from m in api.Methods where m.Category == c select m).ToList();
                var outputPath = Path.Combine(rootFolder, QuickStringMerge(template.output, c));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                template.razor.Category = c;
                var output = template.razor.ExecuteTemplate(api, null, null, null);
                template.razor.Category = null;
                File.WriteAllText(outputPath, output);
                api.Methods = oldMethods;
            }
        }

        private void RenderSingleFile(OkapiInfo api, RenderTemplateTask template)
        {
            var outputPath = Path.Combine(rootFolder, template.output);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var output = template.razor.ExecuteTemplate(api, null, null, null);
            File.WriteAllText(outputPath, output);
        }
        #endregion

        #region Parsing
        public void ParseRazorTemplates(string renderFilePath)
        {
            // Shortcut
            if (templates == null) return;

            // Parse all razor templates
            string templatePath, contents;
            foreach (var template in templates) {
                try {
                    templatePath = Path.Combine(renderFilePath, template.file);
                    Console.WriteLine($"     Parsing template {templatePath}...");
                    contents = File.ReadAllText(templatePath);
                    TestRazorTemplate(contents, templatePath);
                } catch (Exception ex) {
                    Console.WriteLine($"Exception parsing {template.file}: {ex.Message}");
                    throw ex;
                }
            }
        }
        #endregion

        #region Razor Engine Config
        private IRazorLightEngine _engine = null;
        private IRazorLightEngine SetupRazorEngine(string folder)
        {
            if (_engine != null) return _engine;

            // Set up the hosting environment
            var config = EngineConfiguration.Default;
            config.Namespaces.Add("OkapiDoc.TemplateBase");
            config.Namespaces.Add("System");

            // Here you go
            _engine = EngineFactory.CreateCustom(new FilesystemTemplateManager(folder), config);
            return _engine;
        }

        protected void TestRazorTemplate(string template, string path)
        {
            // Construct a razor templating engine and a compiler
            var templateFolder = Path.GetDirectoryName(path);
            var engine = SetupRazorEngine(templateFolder);
            var codeProvider = new CSharpCodeProvider();

            // Produce generator results for all templates
            string code = null;
            using (var r = new StringReader(template)) {

                // Produce analyzed code
                code = r.ReadToEnd();
                var results = engine.ParseString(code, new OkapiInfo());
            }
        }

        /// <summary>
        /// Replace a regex in a file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="oldRegex"></param>
        /// <param name="newString"></param>
        /// <param name="encoding"></param>
        protected void ReplaceStringInFile(string path, string oldRegex, string newString, Encoding encoding)
        {
            // Read in the global assembly info file
            string contents = File.ReadAllText(path, System.Text.Encoding.UTF8);

            // Replace assembly version and assembly file version
            Regex r = new Regex(oldRegex);
            contents = r.Replace(contents, newString);

            // Write the file back
            File.WriteAllText(path, contents, encoding);
        }
        #endregion

        #region String merge function
        private string QuickStringMerge(string template, object mergeSource)
        {
            Regex r = new Regex("{.+?}");
            var matches = r.Matches(template);
            foreach (Match m in matches) {

                // Split into function and field
                string field = m.Value.Substring(1, m.Value.Length - 2);
                string func = null;
                int p = field.IndexOf('.');
                if (p >= 0) {
                    func = field.Substring(p + 1);
                    field = field.Substring(0, p);
                }

                // If we're merging with a plain string, just use that
                string mergeString;
                if (mergeSource is string) {
                    mergeString = mergeSource as string;

                // Find this value in the merge data
                } else {
                    PropertyInfo pi = mergeSource.GetType().GetProperty(field);
                    if (pi == null) {
                        throw new Exception($"Field '{field}' not found when merging filenames.");
                    }
                    object mergeValue = pi.GetValue(mergeSource);
                    mergeString = mergeValue == null ? "" : mergeValue.ToString();
                }

                // Apply function, if any
                switch (func) {
                    case "trim": mergeString = mergeString.Trim(); break;
                    case "lower": mergeString = mergeString.ToLower(); break;
                }

                // Merge this value into the template
                template = template.Replace(m.Value, mergeString);
            }

            // Here's the merged template
            return template;
        }
        #endregion
    }
}
