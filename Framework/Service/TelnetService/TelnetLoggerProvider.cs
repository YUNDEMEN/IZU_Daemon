using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace Wonder.Service
{
    [UnsupportedOSPlatform("browser")]
    [ProviderAlias("ColorConsole")]
    public sealed class TelnetLoggerProvider : ILoggerProvider
    {
        private readonly IDisposable? _onChangeToken;
        private TelnetLoggerConfiguration _currentConfig;
        private readonly ConcurrentDictionary<string, TelnetLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

        public TelnetLoggerProvider(IOptionsMonitor<TelnetLoggerConfiguration> config)
        {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
        }

        public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new TelnetLogger(name, GetCurrentConfig));

        private TelnetLoggerConfiguration GetCurrentConfig() => _currentConfig;

        public void Dispose()
        {
            _loggers.Clear();
            _onChangeToken?.Dispose();
        }
    }
}
