using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Topshelf;
using izu.watcher.moxa;
using NLog.Extensions.Logging;



void LogOnce(string path, string? content)
{
    //if (!dir.Exists) dir.Create();
    Console.WriteLine(content);
    //File.WriteAllText(path, content);
}
var serviceCollection = new ServiceCollection();
serviceCollection.AddLogging(config =>
{
    config.AddNLog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config"));
});
var containerBuilder = new ContainerBuilder();
containerBuilder.Populate(serviceCollection);
containerBuilder.RegisterType<WonderService>();
containerBuilder.RegisterType<MoxaWatcher>().As<IMoxaWatcher>();
var container = containerBuilder.Build();
var serviceProvider = new AutofacServiceProvider(container);

Host host = HostFactory.New(x =>
{
    x.UseAutofacContainer(container);
    x.Service<WonderService>(s =>
    {
        s.ConstructUsingAutofacContainer();
        s.WhenStarted((service, control) => service.Start());
        s.WhenStopped((service, control) => service.Stop());
    });
    
    x.SetServiceName("IZU Watcher (WonderService)");
    x.SetDisplayName("IZU Watcher");
    x.SetDescription("监控网络环境（MOXA)");
#if true
    x.EnableServiceRecovery(r =>
    {
        //if (recoverySeconds > 0.1)
        r.RestartService(TimeSpan.FromSeconds(5));

        //操作限制在2-3个
        //r.RestartComputer(5, "message");
        //r.RestartComputer(1,"restart computer");
        //r.RunProgram(7, "ping www.baidu.com");
        //r.OnCrashOnly();
        //r.SetResetPeriod(1);
    });
#endif
    x.OnException(ex =>
    {
        LogOnce($"{AppDomain.CurrentDomain.BaseDirectory}logs\\{DateTime.Now:yyyyMMddHHmmss}-crash.log", ex.StackTrace);
    });
    x.StartAutomatically();
    x.RunAsLocalSystem();
    //x.RunAsLocalService();
    //x.RunAs("administrator", "duan");
    //x.RunAsPrompt();
});

TopshelfExitCode rc = host.Run();
var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());//11
Environment.ExitCode = exitCode;

