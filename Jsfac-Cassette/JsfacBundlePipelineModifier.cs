using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Cassette;
using Cassette.BundleProcessing;
using Cassette.Scripts;

namespace Jsfac_Cassette
{
    public class JsfacBundlePipelineModifier : IBundlePipelineModifier<ScriptBundle>
    {
        public IBundlePipeline<ScriptBundle> Modify(IBundlePipeline<ScriptBundle> pipeline)
        {
            var index = pipeline.IndexOf<ParseJavaScriptReferences>();
            pipeline.Insert<ParseJsfacReferences>(index + 1);
            return pipeline;
        }
    }

    public class ParseJsfacReferences : ParseReferences<ScriptBundle>
    {
        JsfacFileManager.Item _item;
        protected override bool ShouldParseAsset(IAsset asset)
        {
            if (asset.Path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                _item = JsfacFileManager.GetByPath(asset.Path);
                return _item != null;
            }

            return false;
        }

        protected override ICommentParser CreateCommentParser()
        {
            var parser = new JsfacCommentPareser();
            if (_item != null)
            {
                parser.ReferencedModules = _item.ReferencedModules;
                parser.ReferencedServices = _item.ReferencedServices;
            }
            return parser;
        }
    }

    public class JsfacCommentPareser : ICommentParser
    {
        public List<string> ReferencedModules = new List<string>();
        public List<string> ReferencedServices = new List<string>();  
        public IEnumerable<Comment> Parse(string code)
        {
            var modules = ReferencedModules;
            var services = ReferencedServices;

            foreach (var module in modules)
            {
                foreach (var service in services)
                {
                    var path = JsfacFileManager.GetPath(module, service);
                    if (path != null)
                    {
                        var c = new Comment
                        {
                            LineNumber = 0, //todo: do we need to specify this?
                            Value = "@reference " + path
                        };

                        yield return c;
                    }
                }
            }
        }
    }

    //Note: temporary solution to resolve jsfac info from js file
    public static class JsfacFileManager
    {
        public static string GetPath(string module, string name)
        {
            Item itm;
            if (_map.TryGetValue(Tuple.Create(module, name), out itm))
            {
                return itm.Path;
            }

            return null;
        }

        public static Item GetByPath(string path)
        {
            Item itm;
            if (_path.TryGetValue(path, out itm))
            {
                return itm;
            }

            return null;
        }

        public static void Init(string virtualJsRoot)
        {
            var root = System.Web.Hosting.HostingEnvironment.MapPath(virtualJsRoot);
            Traverse(root, (path) =>
            {
                var content = File.ReadAllText(path);
                var itm = Parse(content);
                if (itm == null)
                    return;

                itm.Path = (virtualJsRoot + path.Replace(root, string.Empty)).Replace('\\', '/');

                _map[Tuple.Create(itm.Module, itm.Name)] = itm;
                _path[itm.Path] = itm;
            });
        }

        public static List<string> GetReferencedModules(string content)
        {
            var m = FindReferencedModules.Match(content);
            if (!m.Success)
                return new List<string>();

            var r = ExtractStringsFromArray(m.Groups[1].Value);

            return r;
        }

        public static List<string> GetReferencedServices(string content)
        {
            var m = FindReferencedServices.Match(content);
            if (!m.Success)
                return new List<string>();

            var r = ExtractStringsFromArray(m.Groups[1].Value);

            return r;
        }

        static List<string> ExtractStringsFromArray(string content)
        {
            var lst = new List<string>();
            foreach (var itm in content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = itm.Trim();
                lst.Add(s.Substring(1, s.Length - 2));
            }

            return lst;
        } 

        static Regex FindReferencedModules = new Regex(@"jsfac\s*\.\s*module\s*\(\s*['""]\s*[^'""]+\s*['""]\s*,\s*\[\s*([^]]+)\s*\]", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
        static Regex FindReferencedServices = new Regex(@"register\s*\(\s*['""]\s*[^'""]+\s*['""]\s*,\s*\[\s*([^]]+)\s*\]", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
        static Dictionary<Tuple<string, string>, Item> _map = new Dictionary<Tuple<string, string>, Item>();
        static Dictionary<string, Item> _path = new Dictionary<string, Item>();

        public class Item
        {
            public string Path;
            public string Module;
            public string Name;
            public List<string> ReferencedModules = new List<string>(); 
            public List<string> ReferencedServices = new List<string>(); 
        }

        static Regex FindModule = new Regex(@"jsfac\s*\.\s*module\s*\(\s*['""]\s*([^'""]+)\s*['""]", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
        static Regex FindService = new Regex(@"register\s*\(\s*['""]\s*([^'""]+)\s*['""]", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);

        static Item Parse(string content)
        {
            Item r = null;
            var m = FindModule.Match(content);
            if (!m.Success)
                return r;

            var module = m.Groups[1].Value;

            m = FindService.Match(content);

            if (!m.Success)
                return r;

            var referencedModules = GetReferencedModules(content);
            referencedModules.Add(module);
            var referencedServices = GetReferencedServices(content);

            var name = m.Groups[1].Value;

            return new Item { Name = name, Module = module, ReferencedModules = referencedModules, ReferencedServices = referencedServices};
        }

        static void Traverse(string path, Action<string> callback)
        {
            var files = Directory.GetFiles(path, "*.js", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                callback(file);
            }
        }
    }
}
