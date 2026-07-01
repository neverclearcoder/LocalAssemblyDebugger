using System.Net;
using LocalAssemblyDebugger.Scenarios;
using LocalAssemblyDebugger.UI;
using Spectre.Console;

namespace LocalAssemblyDebugger
{
    public class App
    {
        public void Run()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var scenarioService = new ScenarioService();
            OfferAppConfigImport(scenarioService);

            var mainMenu     = new MainMenu();
            var pluginMenu   = new PluginMenu(scenarioService);
            var caMenu       = new CodeActivityMenu(scenarioService);
            var scenarioMenu = new ScenarioMenu(scenarioService);

            while (true)
            {
                MainMenuChoice choice = mainMenu.Show();

                switch (choice)
                {
                    case MainMenuChoice.Plugin:
                        pluginMenu.Run();
                        break;

                    case MainMenuChoice.CodeActivity:
                        caMenu.Run();
                        break;

                    case MainMenuChoice.LoadScenario:
                        ScenarioRunResult result = scenarioMenu.Show();
                        if (result != null)
                        {
                            if (result.IsPlugin)
                                pluginMenu.Run(result.PluginScenario);
                            else
                                caMenu.Run(result.CodeActivityScenario);
                        }
                        break;

                    case MainMenuChoice.Exit:
                        AnsiConsole.MarkupLine("[grey]Exiting...[/]");
                        return;
                }

                AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
                System.Console.ReadKey(true);
            }
        }

        private void OfferAppConfigImport(ScenarioService service)
        {
            if (!service.ShouldOfferAppConfigImport()) return;

            AnsiConsole.MarkupLine("[yellow]Legacy settings detected in App.config.[/]");
            if (AnsiConsole.Confirm("Import legacy settings as a scenario?", true))
            {
                service.ImportFromAppConfig();
                AnsiConsole.MarkupLine("[green]Settings imported.[/]");
            }
        }
    }
}
