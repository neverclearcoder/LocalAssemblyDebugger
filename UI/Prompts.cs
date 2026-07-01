using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LocalAssemblyDebugger.Scenarios;
using Microsoft.Xrm.Sdk;
using Spectre.Console;

namespace LocalAssemblyDebugger.UI
{
    public static class Prompts
    {
        private static readonly string[] AttributeTypes =
        {
            "string", "int", "bool", "decimal", "guid",
            "entityref", "optionset", "money", "datetime"
        };

        private static readonly string[] ParameterTypes =
        {
            "string", "int", "bool", "decimal", "guid", "entityref"
        };

        public static string Ask(string label, string defaultValue = "")
        {
            var tp = new TextPrompt<string>(label + ":");
            if (!string.IsNullOrEmpty(defaultValue))
                tp.DefaultValue(defaultValue);
            tp.AllowEmpty();
            return (AnsiConsole.Prompt(tp) ?? "").Trim();
        }

        public static Guid AskGuid(string label, Guid defaultValue = default)
        {
            while (true)
            {
                string raw = Ask(label, defaultValue == Guid.Empty ? "" : defaultValue.ToString());
                if (string.IsNullOrWhiteSpace(raw) && defaultValue != Guid.Empty)
                    return defaultValue;
                if (Guid.TryParse(raw, out Guid g))
                    return g;
                AnsiConsole.MarkupLine("[red]Invalid GUID. Example: 3fa85f64-0000-0000-0000-000000000001[/]");
            }
        }

        public static int AskInt(string label, int defaultValue)
        {
            while (true)
            {
                string raw = Ask(label, defaultValue.ToString());
                if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
                if (int.TryParse(raw, out int n)) return n;
                AnsiConsole.MarkupLine("[red]Invalid integer.[/]");
            }
        }

        public static List<InputParameter> AskAttributes(string title, List<InputParameter> defaults = null)
        {
            var result = defaults != null ? new List<InputParameter>(defaults) : new List<InputParameter>();

            AnsiConsole.MarkupLine($"\n[cyan]{Markup.Escape(title)}[/]");
            AnsiConsole.MarkupLine("[grey]Supported types: string int bool decimal guid entityref optionset money datetime[/]");
            AnsiConsole.MarkupLine("[grey]entityref format: logicalname,3fa85f64-...[/]");
            AnsiConsole.MarkupLine("[grey]Empty name -> finish[/]");

            if (result.Count > 0)
            {
                var tbl = new Table().Border(TableBorder.Minimal)
                    .AddColumn("Attribute").AddColumn("Type").AddColumn("Value");
                foreach (var p in result)
                    tbl.AddRow(Markup.Escape(p.Name), Markup.Escape(p.Type),
                        Markup.Escape(p.Value ?? ""));
                AnsiConsole.Write(tbl);
            }

            while (true)
            {
                string name = Ask("  Attribute name (empty=finish)");
                if (string.IsNullOrWhiteSpace(name)) break;

                string type = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"  [bold]{Markup.Escape(name)}[/] type:")
                        .AddChoices(AttributeTypes));

                string hint = "";
                if (type == "entityref") hint = " [logicalname,guid]";
                else if (type == "datetime") hint = " [yyyy-MM-dd HH:mm:ss]";
                else if (type == "optionset") hint = " [int]";

                string value = Ask($"  {Markup.Escape(name)} value{hint}");

                var existing = result.FirstOrDefault(p => p.Name == name);
                if (existing != null) { existing.Type = type; existing.Value = value; }
                else result.Add(new InputParameter { Name = name, Type = type, Value = value });

                AnsiConsole.MarkupLine($"  [green]+ {Markup.Escape(name)} ({type})[/]");
            }

            return result;
        }

        public static List<InputParameter> AskInputParameterList(List<InputParameter> defaults = null)
        {
            var result = defaults != null ? new List<InputParameter>(defaults) : new List<InputParameter>();

            AnsiConsole.MarkupLine("\n[cyan]Input Parameters[/]");
            AnsiConsole.MarkupLine("[grey]Empty name -> finish[/]");

            if (result.Count > 0)
            {
                var tbl = new Table().Border(TableBorder.Minimal)
                    .AddColumn("Parameter").AddColumn("Type").AddColumn("Value");
                foreach (var p in result)
                    tbl.AddRow(Markup.Escape(p.Name), Markup.Escape(p.Type),
                        Markup.Escape(p.Value ?? ""));
                AnsiConsole.Write(tbl);
            }

            while (true)
            {
                string name = Ask("  Parameter name (empty=finish)");
                if (string.IsNullOrWhiteSpace(name)) break;

                string type = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"  [bold]{Markup.Escape(name)}[/] type:")
                        .AddChoices(ParameterTypes));

                string value = Ask($"  {Markup.Escape(name)} value");

                var existing = result.FirstOrDefault(p => p.Name == name);
                if (existing != null) { existing.Type = type; existing.Value = value; }
                else result.Add(new InputParameter { Name = name, Type = type, Value = value });

                AnsiConsole.MarkupLine($"  [green]+ {Markup.Escape(name)} ({type})[/]");
            }

            return result;
        }

        public static Dictionary<string, object> ConvertParameterList(List<InputParameter> list)
        {
            var result = new Dictionary<string, object>();
            foreach (var p in list)
            {
                try { result[p.Name] = ConvertValue(p.Type, p.Value); }
                catch { result[p.Name] = p.Value; }
            }
            return result;
        }

        public static object ConvertValue(string type, string value)
        {
            switch ((type ?? "string").ToLowerInvariant())
            {
                case "int":      return int.Parse(value);
                case "bool":     return bool.Parse(value);
                case "guid":     return Guid.Parse(value);
                case "decimal":  return decimal.Parse(value, CultureInfo.InvariantCulture);
                case "entityref":
                    var parts = value.Split(',');
                    return new EntityReference(parts[0].Trim(), Guid.Parse(parts[1].Trim()));
                default:         return value;
            }
        }

        public static List<InputParameter> ParametersToList(Dictionary<string, object> inputs)
        {
            var result = new List<InputParameter>();
            if (inputs == null) return result;
            foreach (var kv in inputs)
            {
                string type = "string";
                string value = kv.Value?.ToString() ?? "";
                if      (kv.Value is int)            type = "int";
                else if (kv.Value is bool)           type = "bool";
                else if (kv.Value is Guid)           type = "guid";
                else if (kv.Value is decimal)        type = "decimal";
                else if (kv.Value is EntityReference er)
                {
                    type = "entityref";
                    value = $"{er.LogicalName},{er.Id}";
                }
                result.Add(new InputParameter { Name = kv.Key, Type = type, Value = value });
            }
            return result;
        }
    }
}
