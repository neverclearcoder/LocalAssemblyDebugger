using System.Linq;
using LocalAssemblyDebugger.Scenarios;
using Spectre.Console;

namespace LocalAssemblyDebugger.UI
{
    public class ScenarioRunResult
    {
        public bool                 IsPlugin             { get; set; }
        public PluginScenario       PluginScenario       { get; set; }
        public CodeActivityScenario CodeActivityScenario { get; set; }
    }

    public class ScenarioMenu
    {
        private readonly ScenarioService _service;

        public ScenarioMenu(ScenarioService service)
        {
            _service = service;
        }

        public ScenarioRunResult Show()
        {
            var scenarios = _service.ListAll();

            if (scenarios.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Kayitli senaryo bulunamadi.[/] (scenarios/*.json)");
                AnsiConsole.MarkupLine("[grey]Plugin veya CodeActivity menusu uzerinden calistirip kaydedebilirsiniz.[/]\n");
                return null;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("#")
                .AddColumn("Ad")
                .AddColumn("Tip")
                .AddColumn("Son Degisiklik");

            for (int i = 0; i < scenarios.Count; i++)
            {
                var s = scenarios[i];
                string color = s.Type == "Plugin" ? "cyan" : "magenta";
                table.AddRow(
                    (i + 1).ToString(),
                    $"[{color}]{Markup.Escape(s.Name)}[/]",
                    $"[{color}]{Markup.Escape(s.Type)}[/]",
                    s.Modified.ToString("yyyy-MM-dd HH:mm"));
            }
            AnsiConsole.Write(table);

            var choices = scenarios.Select(s => s.Name).ToList();
            choices.Add("<- Geri");

            string selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Senaryo secin:")
                    .AddChoices(choices));

            if (selected == "<- Geri") return null;

            var info = scenarios.First(s => s.Name == selected);

            string action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold]{Markup.Escape(selected)}[/] icin islem:")
                    .AddChoices("Calistir", "Sil", "<- Geri"));

            if (action == "<- Geri") return null;

            if (action == "Sil")
            {
                if (AnsiConsole.Confirm($"[yellow]{Markup.Escape(selected)}[/] silinsin mi?", false))
                {
                    _service.Delete(info.FilePath);
                    AnsiConsole.MarkupLine("[green]Senaryo silindi.[/]");
                }
                return null;
            }

            if (info.Type == "Plugin")
                return new ScenarioRunResult
                {
                    IsPlugin = true,
                    PluginScenario = _service.LoadPlugin(info.FilePath)
                };

            return new ScenarioRunResult
            {
                IsPlugin = false,
                CodeActivityScenario = _service.LoadCodeActivity(info.FilePath)
            };
        }
    }
}
