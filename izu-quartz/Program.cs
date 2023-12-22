
using izu.quartz;
using Quartz;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory
    //WindowsServiceHelpers.IsWindowsService() ? AppDomain.CurrentDomain.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);
builder.Host.UseWindowsService();
builder.Services.AddQuartz(quartz=>
{
    quartz.UseXmlSchedulingConfiguration(x =>
    {
        x.Files = new[] { "~/quartz_jobs.xml" };
        x.ScanInterval = TimeSpan.FromMinutes(1);
        x.FailOnFileNotFound = true;
        x.FailOnSchedulingError = true;
    });
});
builder.Services.AddQuartzHostedService(options =>
{
    // when shutting down we want jobs to complete gracefully
    options.WaitForJobsToComplete = true;

    // when we need to init another IHostedServices first
    options.StartDelay = TimeSpan.FromSeconds(10);
});

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.Run();
