using Quartz;

namespace job_moxa
{
    public class JobMoxa : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context)
        {
            //logger.LogInformation("SampleJob running...");
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            //logger.LogInformation("SampleJob run finished.");
        }
    }
}
