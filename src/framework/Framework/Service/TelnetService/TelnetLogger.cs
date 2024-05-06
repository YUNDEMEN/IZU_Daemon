using Microsoft.Extensions.Logging;
using System;

namespace Wonder.Service
{
    public sealed class TelnetLogger : ILogger
    {
        string name;
        Func<TelnetLogConfiguration> getCurrentConfig;
        public TelnetLogger(string name, Func<TelnetLogConfiguration> getCurrentConfig)
        {
            this.getCurrentConfig = getCurrentConfig;
            this.name = name;
        }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel) => getCurrentConfig().LogLevelToColorMap.ContainsKey(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            WonderTelnetService.TelnetService?.PostLog($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {logLevel.ToString(),-12}: {name} - {formatter(state, exception)}");
        }
        public void LogStill<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            TelnetLogConfiguration config = getCurrentConfig();
            if (config.EventId == 0 || config.EventId == eventId.Id)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                //Console.WriteLine($"[{eventId.Id,2}: {logLevel,-12}]");
                Console.ForegroundColor = originalColor;
                Console.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ");
                Console.ForegroundColor = config.LogLevelToColorMap[logLevel];
                Console.Write($"[{logLevel.ToString().ToUpper()}]：{name} - {formatter(state, exception)}");
                Console.ForegroundColor = originalColor;
                Console.WriteLine();
            }
        }
    }
}
