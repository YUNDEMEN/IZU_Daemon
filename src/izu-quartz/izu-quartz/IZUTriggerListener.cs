using Quartz;
using Quartz.Listener;

namespace izu.quartz;

public class IZUTriggerListener : TriggerListenerSupport
{
    private readonly ILogger<IZUTriggerListener> logger;

    public IZUTriggerListener(ILogger<IZUTriggerListener> logger)
    {
        this.logger = logger;
    }

    public override string Name => "Sample Trigger Listener";

    public override ValueTask TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        //logger.LogInformation("Observed trigger fire by trigger {TriggerKey}", trigger.Key);
        return default;
    }
}