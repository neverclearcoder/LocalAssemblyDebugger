using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LocalAssemblyDebugger.Scenarios
{
    public class ScenarioInfo
    {
        public string   Name     { get; set; }
        public string   Type     { get; set; }
        public DateTime Modified { get; set; }
        public string   FilePath { get; set; }
    }

    public class ScenarioService
    {
        private const string Dir = "scenarios";

        public ScenarioService()
        {
            Directory.CreateDirectory(Dir);
        }

        public void Save(PluginScenario scenario)
        {
            File.WriteAllText(GetPath(scenario.Name),
                JsonConvert.SerializeObject(scenario, Formatting.Indented));
        }

        public void Save(CodeActivityScenario scenario)
        {
            File.WriteAllText(GetPath(scenario.Name),
                JsonConvert.SerializeObject(scenario, Formatting.Indented));
        }

        public IReadOnlyList<ScenarioInfo> ListAll()
        {
            if (!Directory.Exists(Dir))
                return new List<ScenarioInfo>();

            return Directory.GetFiles(Dir, "*.json")
                .Select(f =>
                {
                    try
                    {
                        var obj = JObject.Parse(File.ReadAllText(f));
                        return new ScenarioInfo
                        {
                            Name     = obj["name"]?.ToString() ?? Path.GetFileNameWithoutExtension(f),
                            Type     = obj["type"]?.ToString() ?? "Unknown",
                            Modified = File.GetLastWriteTime(f),
                            FilePath = f
                        };
                    }
                    catch
                    {
                        return new ScenarioInfo
                        {
                            Name     = Path.GetFileNameWithoutExtension(f),
                            Type     = "Unknown",
                            Modified = File.GetLastWriteTime(f),
                            FilePath = f
                        };
                    }
                })
                .OrderByDescending(x => x.Modified)
                .ToList();
        }

        public PluginScenario LoadPlugin(string filePath)
        {
            return JsonConvert.DeserializeObject<PluginScenario>(File.ReadAllText(filePath));
        }

        public CodeActivityScenario LoadCodeActivity(string filePath)
        {
            return JsonConvert.DeserializeObject<CodeActivityScenario>(File.ReadAllText(filePath));
        }

        public void Delete(string filePath)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public bool ShouldOfferAppConfigImport()
        {
            bool hasAppConfig = !string.IsNullOrWhiteSpace(
                ConfigurationManager.AppSettings["AssemblyPath"]);
            bool alreadyImported = File.Exists(GetPath("imported_from_appconfig"));
            return hasAppConfig && !alreadyImported;
        }

        public PluginScenario ImportFromAppConfig()
        {
            Guid.TryParse(ConfigurationManager.AppSettings["TargetEntityId"], out Guid entityId);
            bool.TryParse(ConfigurationManager.AppSettings["RetrieveEntity"], out bool retrieve);

            return new PluginScenario
            {
                Name             = "imported_from_appconfig",
                AssemblyPath     = ConfigurationManager.AppSettings["AssemblyPath"]     ?? "",
                ConnectionString = ConfigurationManager.ConnectionStrings["CrmConnection"]
                                       ?.ConnectionString ?? "",
                EntityName       = ConfigurationManager.AppSettings["TargetEntityName"] ?? "",
                EntityId         = entityId,
                MessageName      = ConfigurationManager.AppSettings["MessageName"]      ?? "Create",
                RetrieveEntity   = retrieve
            };
        }

        private static string GetPath(string name)
        {
            return Path.Combine(Dir, Slugify(name) + ".json");
        }

        public static string Slugify(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";
            string s = name.ToLowerInvariant().Trim();
            s = s.Replace("ı", "i").Replace("ğ", "g")
                 .Replace("ü", "u").Replace("ş", "s")
                 .Replace("ö", "o").Replace("ç", "c")
                 .Replace("İ", "i").Replace("Ğ", "g")
                 .Replace("Ü", "u").Replace("Ş", "s")
                 .Replace("Ö", "o").Replace("Ç", "c")
                 .Replace(' ', '_');
            return Regex.Replace(s, @"[^\w]", "");
        }
    }
}
