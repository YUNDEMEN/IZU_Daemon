using IZU.Interfaces;
using Wonder.Service.Framework;

namespace IZU.Service
{
    [Regist(RegisterTypes.HostedService)]
    public sealed class MainBackgroundService : BackgroundService
    {
        readonly IIZUService _izuService;
        readonly IEnumerable<ILongRunningTask> _runningTasks;
        readonly ILogger _logger;
        public MainBackgroundService(ILogger<MainBackgroundService> logger, IServiceProvider serviceProvider, IIZUService izuService, IEnumerable<ILongRunningTask> tasks)
        {
            _logger = logger;
            _izuService = izuService;
            _runningTasks = tasks;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("---------------IZU service starting---------------");
            await _izuService.StartAsync();

            foreach (var task in _runningTasks)
            {
                task.Start();
            }
            _logger.LogInformation("---------------IZU service started---------------");
        }
    }


}
