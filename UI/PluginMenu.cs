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

            AnsiConsole.MarkupLine("\n[bold cyan]== Run Plugin ==[/]");

            // Step 1: Assembly path
            string assemblyPath = Prompts.Ask("Assembly path (.dll)", scenario.AssemblyPath);
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                AnsiConsole.MarkupLine("[red]Assembly path is required.[/]"); return;
            }
            scenario.AssemblyPath = assemblyPath;

            // Step 2: Pick plugin class
            Type pluginType = PickPluginType(assemblyPath);
            if (pluginType == null) return;
            scenario.ClassName = pluginType.FullName;

            // Step 3: Connection string
            string conn = Prompts.Ask("CRM connection string", scenario.ConnectionString);
            if (string.IsNullOrWhiteSpace(conn))
            {
                AnsiConsole.MarkupLine("[red]Connection string is required.[/]"); return;
            }
            scenario.ConnectionString = conn;

            // Step 4: Entity / message config
            scenario.EntityName   = Prompts.Ask("Entity logical name", scenario.EntityName);
            scenario.EntityId     = Prompts.AskGuid("Entity ID (GUID)", scenario.EntityId);
            scenario.MessageName  = Prompts.Ask("Message name (Create/Update/...)", scenario.MessageName);
            scenario.Stage        = Prompts.AskInt("Stage (10=Pre-Val,20=Pre-Op,40=Post-Op)", scenario.Stage);
            scenario.Mode         = Prompts.AskInt("Mode (0=Sync,1=Async)", scenario.Mode);
            scenario.Depth        = Prompts.AskInt("Depth", scenario.Depth);
            scenario.UnsecureConfig = Prompts.Ask("UnsecureConfig (empty=none)", scenario.UnsecureConfig);
            scenario.SecureConfig  = Prompts.Ask("SecureConfig (empty=none)", scenario.SecureConfig);

            // Step 5: Target attributes
            scenario.TargetAttributes = Prompts.AskAttributes("Target Entity Attributes", scenario.TargetAttributes);

            // Step 6: Images
            ConfigureImages(scenario);

            // Step 7: Save?
            if (AnsiConsole.Confirm("Save these settings as a scenario?", !fromScenario))
            {
                string name = Prompts.Ask("Scenario name", scenario.Name);
                scenario.Name = string.IsNullOrWhiteSpace(name) ? scenario.ClassName : name;
                _service.Save(scenario);
                AnsiConsole.MarkupLine($"[green]Scenario saved: {Markup.Escape(scenario.Name)}[/]");
            }

            // Execute
            if (!AnsiConsole.Confirm("Run now?", true)) return;

            Execute(scenario);
        }

        private Type PickPluginType(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
            {
                AnsiConsole.MarkupLine($"[red]File not found: {Markup.Escape(assemblyPath)}[/]");
                return null;
            }

            Assembly asm;
            try { asm = Assembly.LoadFrom(assemblyPath); }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to load assembly: {Markup.Escape(ex.Message)}[/]");
                return null;
            }

            var pluginTypes = new List<Type>();
            foreach (var t in asm.GetTypes())
                if (typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    pluginTypes.Add(t);

            if (pluginTypes.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No class implementing IPlugin was found.[/]");
                return null;
            }

            var names = new List<string>();
            foreach (var t in pluginTypes) names.Add(t.FullName);

            string selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a plugin class:")
                    .AddChoices(names));

            foreach (var t in pluginTypes)
                if (t.FullName == selected) return t;
            return null;
        }

        private void ConfigureImages(PluginScenario scenario)
        {
            string imageMode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Image management:")
                    .AddChoices(
                        "Continue (no image)",
                        "Define PreImage",
                        "Define PostImage",
                        "Define both",
                        "Retrieve from CRM (by entity ID)"));

            if (imageMode == "Continue (no image)") return;

            if (imageMode == "Retrieve from CRM (by entity ID)")
            {
                scenario.PreImages  = new Dictionary<string, List<InputParameter>>
                    { { "Target", new List<InputParameter> { new InputParameter { Name = "__crmRetrieve__", Type = "bool", Value = "true" } } } };
                AnsiConsole.MarkupLine("[grey]CRM retrieve sentinel added (PreImage:Target).[/]");
                return;
            }

            bool doPreImage  = imageMode == "Define PreImage"  || imageMode == "Define both";
            bool doPostImage = imageMode == "Define PostImage" || imageMode == "Define both";

            if (doPreImage)
            {
                string key = Prompts.Ask("PreImage alias", "Target");
                scenario.PreImages[key] = Prompts.AskAttributes($"PreImage '{key}' attributes",
                    scenario.PreImages.ContainsKey(key) ? scenario.PreImages[key] : null);
            }

            if (doPostImage)
            {
                string key = Prompts.Ask("PostImage alias", "Target");
                scenario.PostImages[key] = Prompts.AskAttributes($"PostImage '{key}' attributes",
                    scenario.PostImages.ContainsKey(key) ? scenario.PostImages[key] : null);
            }
        }

        private void Execute(PluginScenario scenario)
        {
            CrmServiceClient service = null;
            AnsiConsole.Status().Start("Connecting to CRM...", ctx =>
            {
                try { service = CrmConnector.Connect(scenario.ConnectionString); }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Connection error: {Markup.Escape(ex.Message)}[/]");
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
                    AnsiConsole.Status().Start("Running plugin...", ctx =>
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
                    AnsiConsole.MarkupLine($"[red bold]Plugin error:[/] {Markup.Escape(ex.Message)}");
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
                        AnsiConsole.MarkupLine($"[grey]CRM retrieve: {attrs.Count} attribute(s) ({kv.Key})[/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[yellow]CRM retrieve failed ({kv.Key}): {Markup.Escape(ex.Message)}[/]");
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
            AnsiConsole.MarkupLine("\n[green bold]Plugin completed successfully.[/]");

            if (context.OutputParameters.Count > 0)
            {
                var tbl = new Table().Border(TableBorder.Rounded)
                    .Title("[bold]Output Parameters[/]")
                    .AddColumn("Parameter").AddColumn("Value");
                foreach (var kv in context.OutputParameters)
                    tbl.AddRow(Markup.Escape(kv.Key), Markup.Escape(kv.Value?.ToString() ?? "(null)"));
                AnsiConsole.Write(tbl);

                logger.WriteInfo("[bold]--- Output Parameters ---[/]");
                foreach (var kv in context.OutputParameters)
                    logger.WriteInfo($"  {Markup.Escape(kv.Key)} = {Markup.Escape(kv.Value?.ToString() ?? "")}");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]No output parameters.[/]");
            }
        }
    }
}
