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
        private const string ChoicePlugin   = "Run Plugin";
        private const string ChoiceCA       = "Run Custom Action (CodeActivity)";
        private const string ChoiceScenario = "Load Scenario";
        private const string ChoiceExit     = "Exit";

        public MainMenuChoice Show()
        {
            AnsiConsole.Clear();

            AnsiConsole.Write(new FigletText("LocalDebugger") { Color = Color.Blue });
            AnsiConsole.MarkupLine("[grey]Dynamics 365 Plugin / CodeActivity Local Debugger v2.0[/]\n");

            string choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]What would you like to do?[/]")
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
