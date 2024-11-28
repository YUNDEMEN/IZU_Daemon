using Quartz;
using Quartz.AspNetCore;
using Quartz.Impl;

namespace izu.moxa
{
    /// <summary>
    /// ≈‰÷√∆Ù∂Ø∂® ±∆˜
    /// </summary>
    public static class QuartzSchedulerSetup
    {
        public static void AddQuartz(this IServiceCollection service, IConfiguration configuration)
        {
            service.AddQuartz(async q =>
            {

                var jobKey = new JobKey("MoxaJob");
                q.AddJob<MoxaJob>(opts => opts.WithIdentity(jobKey));
                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("MoxaJob-trigger")
                    .WithSimpleSchedule(x => x
                    .WithInterval(new TimeSpan(0, 0, 10))
                    .RepeatForever())
                );

            });
            // ASP.NET Core hosting
            service.AddQuartzServer(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });
        }
    }
}