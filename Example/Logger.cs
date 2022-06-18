using Discord;
using System;
using System.Threading.Tasks;

namespace Example
{
    public static class Logger
    {
        public static Task Log(LogMessage logMessage)
        {
            Console.ForegroundColor = SeverityToConsoleColor(logMessage.Severity);
            Console.WriteLine($"{DateTime.Now:dd/MM. H:mm:ss} [{logMessage.Source}] {logMessage.Message}");
            Console.ResetColor();

            return Task.CompletedTask;
        }

        public static Task Log(string source, string message, LogSeverity severity)
        {
            Console.ForegroundColor = SeverityToConsoleColor(severity);
            Console.WriteLine($"{DateTime.Now:dd/MM. H:mm:ss} [{source}] {message}");
            Console.ResetColor();

            return Task.CompletedTask;
        }

        private static ConsoleColor SeverityToConsoleColor(LogSeverity severity)
        {
            return severity switch
            {
                LogSeverity.Critical => ConsoleColor.Red,
                LogSeverity.Debug => ConsoleColor.Blue,
                LogSeverity.Error => ConsoleColor.Yellow,
                LogSeverity.Info => ConsoleColor.Cyan,
                LogSeverity.Verbose => ConsoleColor.Green,
                LogSeverity.Warning => ConsoleColor.Magenta,
                _ => ConsoleColor.White,
            };
        }
    }
}
