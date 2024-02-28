using IZU.Interfaces;
using Wonder;

namespace IZU.Service
{
    public sealed class MainBackgroundService : BackgroundService
    {
        readonly IIZUService _izuService;
        readonly IEnumerable<ILongRunningTask> _runningTasks;
        public MainBackgroundService(IIZUService izuService, IEnumerable<ILongRunningTask> tasks)
        {
            _izuService = izuService;
            _runningTasks = tasks;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _izuService.StartAsync();

            foreach (var task in _runningTasks)
            {
                task.Start();
            }
        }
    }


}
