using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LocalAssemblyDebugger.Features;
using LocalAssemblyDebugger.Fakes;
using LocalAssemblyDebugger.Logging;
using LocalAssemblyDebugger.Scenarios;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using Spectre.Console;

namespace LocalAssemblyDebugger.UI
{
    public class PluginMenu
    {
        private readonly ScenarioService _service;

        public PluginMenu(ScenarioService service)
        {
            _service = service;
        }

        public void Run(PluginScenario scenario = null)
        {
            bool fromScenario = scenario != null;
            if (scenario == null) scenario = new PluginScenario();

            AnsiConsole.MarkupLine("\n[bold cyan]== Plugin Calistir ==[/]");

            // Step 1: Assembly path
            string assemblyPath = Prompts.Ask("Assembly yolu (.dll)", scenario.AssemblyPath);
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                AnsiConsole.MarkupLine("[red]Assembly yolu zorunlu.[/]"); return;
            }
            scenario.AssemblyPath = assemblyPath;

            // Step 2: Pick plugin class
            Type pluginType = PickPluginType(assemblyPath);
            if (pluginType == null) return;
            scenario.ClassName = pluginType.FullName;

            // Step 3: Connection string
            string conn = Prompts.Ask("CRM baglanti dizesi", scenario.ConnectionString);
            if (string.IsNullOrWhiteSpace(conn))
            {
                AnsiConsole.MarkupLine("[red]Baglanti dizesi zorunlu.[/]"); return;
            }
            scenario.ConnectionString = conn;

            // Step 4: Entity / message config
            scenario.EntityName   = Prompts.Ask("Entity logical name", scenario.EntityName);
            scenario.EntityId     = Prompts.AskGuid("Entity ID (GUID)", scenario.EntityId);
            scenario.MessageName  = Prompts.Ask("Mesaj adi (Create/Update/...)", scenario.MessageName);
            scenario.Stage        = Prompts.AskInt("Stage (10=Pre-Val,20=Pre-Op,40=Post-Op)", scenario.Stage);
            scenario.Mode         = Prompts.AskInt("Mode (0=Sync,1=Async)", scenario.Mode);
            scenario.Depth        = Prompts.AskInt("Depth", scenario.Depth);
            scenario.UnsecureConfig = Prompts.Ask("UnsecureConfig (bos=yok)", scenario.UnsecureConfig);
            scenario.SecureConfig  = Prompts.Ask("SecureConfig (bos=yok)", scenario.SecureConfig);

            // Step 5: Target attributes
            scenario.TargetAttributes = Prompts.AskAttributes("Target Entity Attribute'lari", scenario.TargetAttributes);

            // Step 6: Images
            ConfigureImages(scenario);

            // Step 7: Save?
            if (AnsiConsole.Confirm("Bu ayarlari senaryo olarak kaydet?", !fromScenario))
            {
                string name = Prompts.Ask("Senaryo adi", scenario.Name);
                scenario.Name = string.IsNullOrWhiteSpace(name) ? scenario.ClassName : name;
                _service.Save(scenario);
                AnsiConsole.MarkupLine($"[green]Senaryo kaydedildi: {Markup.Escape(scenario.Name)}[/]");
            }

            // Execute
            if (!AnsiConsole.Confirm("Calistir?", true)) return;

            Execute(scenario);
        }

        private Type PickPluginType(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
            {
                AnsiConsole.MarkupLine($"[red]Dosya bulunamadi: {Markup.Escape(assemblyPath)}[/]");
                return null;
            }

            Assembly asm;
            try { asm = Assembly.LoadFrom(assemblyPath); }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Assembly yuklenemedi: {Markup.Escape(ex.Message)}[/]");
                return null;
            }

            var pluginTypes = new List<Type>();
            foreach (var t in asm.GetTypes())
                if (typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    pluginTypes.Add(t);

            if (pluginTypes.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]IPlugin implement eden sinif bulunamadi.[/]");
                return null;
            }

            var names = new List<string>();
            foreach (var t in pluginTypes) names.Add(t.FullName);

            string selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Plugin sinifi secin:")
                    .AddChoices(names));

            foreach (var t in pluginTypes)
                if (t.FullName == selected) return t;
            return null;
        }

        private void ConfigureImages(PluginScenario scenario)
        {
            string imageMode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Image yonetimi:")
                    .AddChoices(
                        "Devam et (image yok)",
                        "PreImage tanimla",
                        "PostImage tanimla",
                        "Her ikisini tanimla",
                        "CRM'den al (entity ID ile)"));

            if (imageMode == "Devam et (image yok)") return;

            if (imageMode == "CRM'den al (entity ID ile)")
            {
                scenario.PreImages  = new Dictionary<string, List<InputParameter>>
                    { { "Target", new List<InputParameter> { new InputParameter { Name = "__crmRetrieve__", Type = "bool", Value = "true" } } } };
                AnsiConsole.MarkupLine("[grey]CRM retrieve sentinel eklendi (PreImage:Target).[/]");
                return;
            }

            bool doPreImage  = imageMode == "PreImage tanimla"  || imageMode == "Her ikisini tanimla";
            bool doPostImage = imageMode == "PostImage tanimla" || imageMode == "Her ikisini tanimla";

            if (doPreImage)
            {
                string key = Prompts.Ask("PreImage alias", "Target");
                scenario.PreImages[key] = Prompts.AskAttributes($"PreImage '{key}' attribute'lari",
                    scenario.PreImages.ContainsKey(key) ? scenario.PreImages[key] : null);
            }

            if (doPostImage)
            {
                string key = Prompts.Ask("PostImage alias", "Target");
                scenario.PostImages[key] = Prompts.AskAttributes($"PostImage '{key}' attribute'lari",
                    scenario.PostImages.ContainsKey(key) ? scenario.PostImages[key] : null);
            }
        }

        private void Execute(PluginScenario scenario)
        {
            CrmServiceClient service = null;
            AnsiConsole.Status().Start("CRM'e baglaniliyor...", ctx =>
            {
                try { service = CrmConnector.Connect(scenario.ConnectionString); }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Baglanti hatasi: {Markup.Escape(ex.Message)}[/]");
                }
            });
            if (service == null) return;

            Guid userId = Guid.Empty;
            try { userId = CrmConnector.GetCurrentUserId(service); }
            catch { }

            var resolvedPreImages  = ResolveImages(scenario.PreImages,  service, scenario.EntityName, scenario.EntityId);
            var resolvedPostImages = ResolveImages(scenario.PostImages, service, scenario.EntityName, scenario.EntityId);

            var target = new Entity(scenario.EntityName, scenario.EntityId);
            PluginExecutor.ApplyAttributes(target, scenario.TargetAttributes);

            var context = new PluginExecutionContextFake
            {
                PrimaryEntityName = scenario.EntityName,
                PrimaryEntityId   = scenario.EntityId,
                MessageName       = scenario.MessageName,
                Stage             = scenario.Stage,
                Mode              = scenario.Mode,
                Depth             = scenario.Depth,
                InitiatingUserId  = userId,
                UserId            = userId,
                OrganizationId    = Guid.NewGuid(),
            };
            context.InputParameters["Target"] = target;

            Assembly asm    = Assembly.LoadFrom(scenario.AssemblyPath);
            Type pluginType = asm.GetType(scenario.ClassName);
            IPlugin plugin  = PluginExecutor.CreateInstance(pluginType, scenario.UnsecureConfig, scenario.SecureConfig);

            string slug = ScenarioService.Slugify(scenario.Name);
            using (var logger = new DebugLogger(slug))
            {
                logger.WriteInfo($"Plugin: {scenario.ClassName}");
                logger.WriteInfo($"Entity: {scenario.EntityName} / {scenario.EntityId} / {scenario.MessageName}");
                logger.WriteInfo($"Stage:{scenario.Stage}  Mode:{scenario.Mode}  Depth:{scenario.Depth}");

                var start = DateTime.Now;
                try
                {
                    AnsiConsole.Status().Start("Plugin calistiriliyor...", ctx =>
                        PluginExecutor.Execute(plugin, service, context, resolvedPreImages, resolvedPostImages, logger));

                    var elapsed = DateTime.Now - start;
                    logger.WriteResult(true, scenario.ClassName, scenario.EntityName, scenario.MessageName, elapsed);
                    ShowResult(context, logger);
                }
                catch (Exception ex)
                {
                    var elapsed = DateTime.Now - start;
                    logger.WriteResult(false, scenario.ClassName, scenario.EntityName, scenario.MessageName, elapsed);
                    logger.WriteError(ex.ToString());
                    AnsiConsole.MarkupLine($"[red bold]Plugin hatasi:[/] {Markup.Escape(ex.Message)}");
                    if (ex.InnerException != null)
                        AnsiConsole.MarkupLine($"[red]Inner: {Markup.Escape(ex.InnerException.Message)}[/]");
                }

                AnsiConsole.MarkupLine($"[grey]Log: {Markup.Escape(logger.LogPath)}[/]");
            }
        }

        private Dictionary<string, List<InputParameter>> ResolveImages(
            Dictionary<string, List<InputParameter>> images,
            CrmServiceClient service,
            string entityName,
            Guid entityId)
        {
            if (images == null || images.Count == 0) return images;

            var resolved = new Dictionary<string, List<InputParameter>>();
            foreach (var kv in images)
            {
                bool isSentinel = kv.Value.Count == 1
                    && kv.Value[0].Name == "__crmRetrieve__"
                    && kv.Value[0].Value == "true";

                if (isSentinel)
                {
                    try
                    {
                        Entity entity = CrmConnector.RetrieveEntity(service, entityName, entityId);
                        var attrs = new List<InputParameter>();
                        foreach (var attr in entity.Attributes)
                            attrs.Add(new InputParameter
                            {
                                Name  = attr.Key,
                                Type  = "string",
                                Value = attr.Value?.ToString() ?? ""
                            });
                        resolved[kv.Key] = attrs;
                        AnsiConsole.MarkupLine($"[grey]CRM retrieve: {attrs.Count} attribute ({kv.Key})[/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[yellow]CRM retrieve basarisiz ({kv.Key}): {Markup.Escape(ex.Message)}[/]");
                        resolved[kv.Key] = new List<InputParameter>();
                    }
                }
                else
                {
                    resolved[kv.Key] = kv.Value;
                }
            }
            return resolved;
        }

        private void ShowResult(PluginExecutionContextFake context, DebugLogger logger)
        {
            AnsiConsole.MarkupLine("\n[green bold]Plugin basariyla tamamlandi.[/]");

            if (context.OutputParameters.Count > 0)
            {
                var tbl = new Table().Border(TableBorder.Rounded)
                    .Title("[bold]Output Parameters[/]")
                    .AddColumn("Parametre").AddColumn("Deger");
                foreach (var kv in context.OutputParameters)
                    tbl.AddRow(Markup.Escape(kv.Key), Markup.Escape(kv.Value?.ToString() ?? "(null)"));
                AnsiConsole.Write(tbl);

                logger.WriteInfo("[bold]--- Output Parameters ---[/]");
                foreach (var kv in context.OutputParameters)
                    logger.WriteInfo($"  {Markup.Escape(kv.Key)} = {Markup.Escape(kv.Value?.ToString() ?? "")}");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]Output parameter yok.[/]");
            }
        }
    }
}
