using Microsoft.Extensions.Logging;

namespace Wonder.Service
{
    public sealed class TelnetLogConfiguration
    {
        public int EventId { get; set; }

        public Dictionary<LogLevel, ConsoleColor> LogLevelToColorMap { get; set; } = new()
        {
            [LogLevel.Information] = ConsoleColor.Green,
            [LogLevel.Warning] = ConsoleColor.Yellow
        };
    }
}
