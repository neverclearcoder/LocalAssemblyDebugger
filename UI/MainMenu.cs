using Spectre.Console;

namespace LocalAssemblyDebugger.UI
{
    public enum MainMenuChoice
    {
        Plugin,
        CodeActivity,
        LoadScenario,
        Exit
    }

    public class MainMenu
    {
        private const string ChoicePlugin   = "Plugin calistir";
        private const string ChoiceCA       = "Custom Action (CodeActivity) calistir";
        private const string ChoiceScenario = "Senaryo yukle";
        private const string ChoiceExit     = "Cikis";

        public MainMenuChoice Show()
        {
            AnsiConsole.Clear();

            AnsiConsole.Write(new FigletText("LocalDebugger") { Color = Color.Blue });
            AnsiConsole.MarkupLine("[grey]Dynamics 365 Plugin / CodeActivity Yerel Hata Ayiklayici v2.0[/]\n");

            string choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Ne yapmak istiyorsunuz?[/]")
                    .AddChoices(ChoicePlugin, ChoiceCA, ChoiceScenario, ChoiceExit));

            switch (choice)
            {
                case ChoicePlugin:   return MainMenuChoice.Plugin;
                case ChoiceCA:       return MainMenuChoice.CodeActivity;
                case ChoiceScenario: return MainMenuChoice.LoadScenario;
                default:             return MainMenuChoice.Exit;
            }
        }
    }
}
