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
                AnsiConsole.MarkupLine("[yellow]No saved scenarios found.[/] (scenarios/*.json)");
                AnsiConsole.MarkupLine("[grey]Run a Plugin or CodeActivity and save it as a scenario to see it here.[/]\n");
                return null;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("#")
                .AddColumn("Name")
                .AddColumn("Type")
                .AddColumn("Last Modified");

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
            choices.Add("<- Back");

            string selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a scenario:")
                    .AddChoices(choices));

            if (selected == "<- Back") return null;

            var info = scenarios.First(s => s.Name == selected);

            string action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Action for [bold]{Markup.Escape(selected)}[/]:")
                    .AddChoices("Run", "Delete", "<- Back"));

            if (action == "<- Back") return null;

            if (action == "Delete")
            {
                if (AnsiConsole.Confirm($"Delete [yellow]{Markup.Escape(selected)}[/]?", false))
                {
                    _service.Delete(info.FilePath);
                    AnsiConsole.MarkupLine("[green]Scenario deleted.[/]");
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
