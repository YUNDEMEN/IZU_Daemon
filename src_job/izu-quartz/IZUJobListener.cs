using Quartz;
using Quartz.Listener;

namespace izu.quartz;

public class IZUJobListener : JobListenerSupport
{
    private readonly ILogger<IZUJobListener> logger;

    public IZUJobListener(ILogger<IZUJobListener> logger)
    {
        this.logger = logger;
    }

    public override string Name => "Sample Job Listener";

    public override ValueTask JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        string jobName = context.JobDetail.Key.Name;
        logger.LogTrace("The job is about to be executed, prepare yourself!");
        return default;
    }
}