using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using NLog.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.AspNetCore;
using Quartz.Logging;

namespace izu.quartz
{
    public static class WonderExtensions
    {
        public static void AddQuartz(this IServiceCollection services, IConfiguration configuration)
        {          
            services.Configure<QuartzOptions>(configuration.GetSection("Quartz"));
            services.AddQuartz(q => {
                // handy when part of cluster or you want to otherwise identify multiple schedulers
                q.SchedulerId = "Scheduler-Core";

                // you can control whether job interruption happens for running jobs when scheduler is shutting down
                q.InterruptJobsOnShutdown = true;

                // when QuartzHostedServiceOptions.WaitForJobsToComplete = true or scheduler.Shutdown(waitForJobsToComplete: true)
                q.InterruptJobsOnShutdownWithWait = true;

                // we can change from the default of 1
                q.MaxBatchSize = 5;
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool(maxConcurrency: 10);

                q.UseTypeLoader<CustomTypeLoader>();

                q.UseXmlSchedulingConfiguration(x =>
                {
                    x.Files = new[] { "~/jobs.xml" };
                    x.ScanInterval = TimeSpan.FromMinutes(1);
                    x.FailOnFileNotFound = true;
                    x.FailOnSchedulingError = true;
                });
                q.AddSchedulerListener<SampleSchedulerListener>();
                q.AddJobListener<SampleJobListener>();
                q.AddTriggerListener<SampleTriggerListener>();
                q.AddHttpApi(options =>
                {
                    // "/quartz-api" is also default value
                    options.ApiPath = "/quartz-api";
                    options.IncludeStackTraceInProblemDetails = true;
                });
                
                q.UsePersistentStore<CustomJobStore>(options =>
                {
                    options.UseNewtonsoftJsonSerializer();
                });
            });

            services.AddOptions<QuartzOptions>();
            services.AddQuartzHostedService(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });
            services.AddQuartzHealthChecks();
            //services.AddSingleton<IWonderJobServer, WonderJobServer>();
        }
        public static async Task UseQuartzAsync(this IWebHost host)
        {
            //host.end.MapQuartzApi()
            //IWonderJobServer wonderJob = host.Services.GetService<IWonderJobServer>();
            //if (wonderJob == null) {

            //}
            //else
            //{
            //   await wonderJob.Initialize();
            //    await wonderJob.Start();
            //}
        }
    }
}
