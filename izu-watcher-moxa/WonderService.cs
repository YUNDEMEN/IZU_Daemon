using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace izu.watcher.moxa
{
    public class WonderService
    {
        readonly ILogger<WonderService> _logger;
        readonly IMoxaWatcher _moxaWatcher;
        public WonderService(ILogger<WonderService> logger, IMoxaWatcher moxaWatcher)
        {
            _logger = logger;
            _moxaWatcher= moxaWatcher;
        }
        public bool Start()
        {
            _logger.LogInformation($"{nameof(WonderService)} started");
            _moxaWatcher.Run();
            return true;
        }

        public bool Stop()
        {
            return true;
        }
    }
}
