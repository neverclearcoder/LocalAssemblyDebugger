using System;
using System.Activities;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LocalAssemblyDebugger.Fakes;
using LocalAssemblyDebugger.Features;
using LocalAssemblyDebugger.Logging;
using LocalAssemblyDebugger.Scenarios;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using Spectre.Console;

namespace LocalAssemblyDebugger.UI
{
    public class CodeActivityMenu
    {
        private readonly ScenarioService _service;

        public CodeActivityMenu(ScenarioService service)
        {
            _service = service;
        }

        public void Run(CodeActivityScenario scenario = null)
        {
            bool fromScenario = scenario != null;
            if (scenario == null) scenario = new CodeActivityScenario();

            AnsiConsole.MarkupLine("\n[bold magenta]== Custom Action (CodeActivity) Calistir ==[/]");

            // Step 1: Assembly
            string assemblyPath = Prompts.Ask("Assembly yolu (.dll)", scenario.AssemblyPath);
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                AnsiConsole.MarkupLine("[red]Assembly yolu zorunlu.[/]"); return;
            }
            scenario.AssemblyPath = assemblyPath;

            // Step 2: Pick CodeActivity class
            Type activityType = PickCodeActivityType(assemblyPath);
            if (activityType == null) return;
            scenario.ClassName = activityType.FullName;

            // Step 3: Connection string
            string conn = Prompts.Ask("CRM baglanti dizesi", scenario.ConnectionString);
            if (string.IsNullOrWhiteSpace(conn))
            {
                AnsiConsole.MarkupLine("[red]Baglanti dizesi zorunlu.[/]"); return;
            }
            scenario.ConnectionString = conn;

            // Step 4: Context entity (optional)
            scenario.EntityName = Prompts.Ask("Entity logical name (bos=yok)", scenario.EntityName);
            if (!string.IsNullOrWhiteSpace(scenario.EntityName))
                scenario.EntityId = Prompts.AskGuid("Entity ID (GUID)", scenario.EntityId);

            // Step 5: Input parameters
            scenario.InputParameters = Prompts.AskInputParameterList(scenario.InputParameters);

            // Save?
            if (AnsiConsole.Confirm("Bu ayarlari senaryo olarak kaydet?", !fromScenario))
            {
                string name = Prompts.Ask("Senaryo adi", scenario.Name);
                scenario.Name = string.IsNullOrWhiteSpace(name) ? scenario.ClassName : name;
                _service.Save(scenario);
                AnsiConsole.MarkupLine($"[green]Senaryo kaydedildi: {Markup.Escape(scenario.Name)}[/]");
            }

            if (!AnsiConsole.Confirm("Calistir?", true)) return;

            Execute(scenario, activityType);
        }

        private Type PickCodeActivityType(string assemblyPath)
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

            var caTypes = new List<Type>();
            foreach (var t in asm.GetTypes())
                if (typeof(CodeActivity).IsAssignableFrom(t) && !t.IsAbstract)
                    caTypes.Add(t);

            if (caTypes.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]CodeActivity sinifi bulunamadi.[/]");
                return null;
            }

            var names = new List<string>();
            foreach (var t in caTypes) names.Add(t.FullName);

            string selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("CodeActivity sinifi secin:")
                    .AddChoices(names));

            foreach (var t in caTypes)
                if (t.FullName == selected) return t;
            return null;
        }

        private void Execute(CodeActivityScenario scenario, Type activityType)
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

            CodeActivity workflow;
            try { workflow = (CodeActivity)Activator.CreateInstance(activityType); }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Sinif olusturulamadi: {Markup.Escape(ex.Message)}[/]");
                return;
            }

            var context = new CodeActivityContextFake
            {
                PrimaryEntityName = scenario.EntityName ?? "",
                PrimaryEntityId   = scenario.EntityId,
                UserId            = userId,
                InitiatingUserId  = userId,
            };

            var inputs = Prompts.ConvertParameterList(scenario.InputParameters);
            string slug = ScenarioService.Slugify(scenario.Name);

            using (var logger = new DebugLogger(slug))
            {
                logger.WriteInfo($"CodeActivity: {scenario.ClassName}");
                logger.WriteInfo($"Entity: {scenario.EntityName} / {scenario.EntityId}");
                logger.WriteInfo($"Input parametreler: {inputs.Count}");

                IDictionary<string, object> outputs = null;

                var start = DateTime.Now;
                try
                {
                    AnsiConsole.Status().Start("CodeActivity calistiriliyor...", ctx =>
                        outputs = CodeActivityExecutor.Execute(workflow, service, context, inputs, logger));

                    var elapsed = DateTime.Now - start;
                    logger.WriteResult(true, scenario.ClassName, scenario.EntityName ?? "", "Execute", elapsed);
                    ShowResult(outputs, logger);
                }
                catch (Exception ex)
                {
                    var elapsed = DateTime.Now - start;
                    logger.WriteResult(false, scenario.ClassName, scenario.EntityName ?? "", "Execute", elapsed);
                    logger.WriteError(ex.ToString());
                    AnsiConsole.MarkupLine($"[red bold]CodeActivity hatasi:[/] {Markup.Escape(ex.Message)}");
                    if (ex.InnerException != null)
                        AnsiConsole.MarkupLine($"[red]Inner: {Markup.Escape(ex.InnerException.Message)}[/]");
                }

                AnsiConsole.MarkupLine($"[grey]Log: {Markup.Escape(logger.LogPath)}[/]");
            }
        }

        private void ShowResult(IDictionary<string, object> outputs, DebugLogger logger)
        {
            AnsiConsole.MarkupLine("\n[green bold]CodeActivity basariyla tamamlandi.[/]");

            if (outputs != null && outputs.Count > 0)
            {
                var tbl = new Table().Border(TableBorder.Rounded)
                    .Title("[bold]Output Parameters[/]")
                    .AddColumn("Parametre").AddColumn("Deger");
                foreach (var kv in outputs)
                    tbl.AddRow(Markup.Escape(kv.Key), Markup.Escape(kv.Value?.ToString() ?? "(null)"));
                AnsiConsole.Write(tbl);

                logger.WriteInfo("[bold]--- Output Parameters ---[/]");
                foreach (var kv in outputs)
                    logger.WriteInfo($"  {Markup.Escape(kv.Key)} = {Markup.Escape(kv.Value?.ToString() ?? "")}");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]Output parameter yok.[/]");
            }
        }
    }
}
