using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace LocalAssemblyDebugger.Logging
{
    public class DebugLogger : IDisposable
    {
        private readonly StreamWriter _file;
        private bool _disposed;

        public DebugLogger(string scenarioName)
        {
            Directory.CreateDirectory("logs");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string slug = Slugify(scenarioName);
            LogPath = Path.Combine("logs", $"{timestamp}_{slug}.log");
            _file = new StreamWriter(LogPath, false, new UTF8Encoding(false));
            WriteFileHeader(scenarioName);
        }

        public string LogPath { get; }

        public void WriteTrace(string message)
        {
            AnsiConsole.MarkupLine($"[grey][[TRACE]][/] {Markup.Escape(message)}");
            _file.WriteLine($"[TRACE] {message}");
            _file.Flush();
        }

        public void WriteInfo(string markup)
        {
            AnsiConsole.MarkupLine(markup);
            _file.WriteLine(StripMarkup(markup));
            _file.Flush();
        }

        public void WriteError(string message)
        {
            AnsiConsole.MarkupLine($"[red]ERROR: {Markup.Escape(message)}[/]");
            _file.WriteLine($"[ERROR] {message}");
            _file.Flush();
        }

        public void WriteResult(bool success, string className, string entityName, string messageName, TimeSpan elapsed)
        {
            _file.WriteLine();
            _file.WriteLine("--- RESULT ---");
            _file.WriteLine($"Status  : {(success ? "Success" : "Failed")}");
            _file.WriteLine($"Class   : {className}");
            _file.WriteLine($"Entity  : {entityName}");
            _file.WriteLine($"Message : {messageName}");
            _file.WriteLine($"Elapsed : {elapsed.TotalMilliseconds:F0}ms");
            _file.Flush();
        }

        private void WriteFileHeader(string scenarioName)
        {
            _file.WriteLine("=== LocalAssemblyDebugger v2.0 ===");
            _file.WriteLine($"Date     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _file.WriteLine($"Scenario : {scenarioName}");
            _file.WriteLine();
            _file.WriteLine("--- TRACE ---");
            _file.Flush();
        }

        private static string StripMarkup(string markup)
        {
            if (markup == null) return "";
            return Regex.Replace(markup, @"\[[^\]]*\]", "");
        }

        private static string Slugify(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";
            string s = name.ToLowerInvariant().Trim()
                .Replace(' ', '_');
            s = s.Replace("i̇", "i").Replace("ı", "i")
                 .Replace("ğ", "g").Replace("ü", "u")
                 .Replace("ş", "s").Replace("ö", "o")
                 .Replace("ç", "c");
            return Regex.Replace(s, @"[^\w]", "");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _file?.Dispose();
        }
    }
}
