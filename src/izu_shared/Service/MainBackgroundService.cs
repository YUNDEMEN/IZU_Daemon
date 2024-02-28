using IZU.Interfaces;
using Wonder;

namespace IZU.Service
{
    public sealed class MainBackgroundService : BackgroundService
    {
        readonly IIZUService _izuService;
        readonly IEnumerable<ILongRunningTask> _runningTasks;
        readonly ILogger _logger;
        public MainBackgroundService(ILogger<MainBackgroundService> logger, IIZUService izuService, IEnumerable<ILongRunningTask> tasks)
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
