using Quartz.Listener;

namespace izu.quartz;

public class IZUSchedulerListener : SchedulerListenerSupport
{
    private readonly ILogger<IZUSchedulerListener> logger;

    public IZUSchedulerListener(ILogger<IZUSchedulerListener> logger)
    {
        this.logger = logger;
    }

    public override ValueTask SchedulerStarted(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Observed scheduler started");
        return default;
    }
}