using Quartz;
namespace izu.quartz;


public class SampleJob : IJob
{
    private readonly ILogger<SampleJob> _logger;
    public SampleJob(ILogger<SampleJob> logger)
    {
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context)
    {
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        _logger.LogInformation($"SampleJob run with trigger {context.Trigger.Key.Name}");
    }
}