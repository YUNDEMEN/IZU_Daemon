using IZU.Interfaces;
using System.CommandLine;
using Wonder.Service.Framework;

namespace IZU.Service
{
    [Regist(RegisterTypes.HostedService)]
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

            if (_runningTasks != null)
            {
                foreach (var task in _runningTasks)
                {
                    try
                    {
                        task.Start();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"{ex.Message}");
                        _logger.LogError($"{ex.StackTrace}");
                    }
                }
            }
            _logger.LogInformation("---------------IZU service started---------------");
        }
    }


}
