using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using IZU.Service;
using Microsoft.Extensions.Hosting.WindowsServices;
using Newtonsoft.Json.Linq;
using NLog.Extensions.Logging;


DirectoryInfo dir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startEx"));
if (dir.Exists) dir.Delete(true);

void LogOnce(string path, string? content)
{
    if (!dir.Exists) dir.Create();
    Console.WriteLine(content);
    File.WriteAllText(path, content);
}

int recoverySeconds = 0;
AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
{
    LogOnce($"{AppDomain.CurrentDomain.BaseDirectory}logs\\{DateTime.Now:yyyyMMddHHmmss}-crash.log", e.ExceptionObject?.ToString());
};

string appsettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
if (!File.Exists(appsettingsPath))
{
    LogOnce($"{dir.FullName}\\startinfo.log", $"config file [{appsettingsPath}] missing!");
    return;
}

string json = File.ReadAllText(appsettingsPath);
try
{
    JObject configJson = JObject.Parse(json);
    var izuNode = configJson["izu"];
    if (izuNode == null)
    {
        LogOnce($"{dir.FullName}\\startinfo.log", "service node not found!");
        return;
    }
    var recoverySecondsNode = izuNode["recoverySeconds"];
    if (recoverySecondsNode == null)
    {
        LogOnce($"{dir.FullName}\\startinfo.log", "recoverySecondsNode not found!");
        return;
    }
    recoverySeconds = recoverySecondsNode.Value<int>();
}
catch (Exception ex)
{
    LogOnce($"{dir.FullName}\\startinfo.log", ex.Message + ex.StackTrace);
    return;
}

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory
    //WindowsServiceHelpers.IsWindowsService()
    //  ? AppDomain.CurrentDomain.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);
builder.Host.UseWindowsService();
if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config")))
{
    Console.WriteLine("NLog config file missing (nlog.config)");
    throw new FileNotFoundException("NLog config file missing (nlog.config)");
}
builder.Logging.AddNLog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config"));
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new DatetimeJsonConverter("yyyy-MM-dd HH:mm:ss"));
}); 
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        name: "AllowAnyOrigin",
        builder =>
        {
            builder.AllowAnyOrigin();
            builder.AllowAnyMethod();
            builder.AllowAnyHeader();
        });
});
builder.Configuration.AddJsonFile("appsettings.json", false, true);
builder.Services.Configure<IZUConfig>(builder.Configuration.GetSection(IZUConfig.KEY));
//builder.Services.BuildServiceProvider()
//.GetRequiredService<IOptionsMonitor<IZUConfig>>()
//.OnChange((profile) =>
//{

//});

builder.Services.AddSingleton<IIZUService, IZUService>();
builder.Services.AddSingleton<IS7NetService, S7NetService>();
var app = builder.Build();
IIZUService serviceInstance = app.Services.GetService<IIZUService>();
serviceInstance?.StartAsync();
app.MapGet("/", () => serviceInstance?.ServiceRuntime);
//app.UseAuthorization();
app.MapControllers();
app.UseBroadcastServer(serviceInstance);
await app.RunAsync();




//Topshelf.Host host = HostFactory.New(x =>
//{
//	x.Service<IZUDaemon>(s =>
//    {
//		s.ConstructUsing(ser => new IZUDaemon());
//		s.WhenStarted((service, control) => {
//            return service.Start();
//		});
//		s.WhenStopped((service, control) => service.Stop());
//	});
//	x.SetServiceName("IZU Daemon");
//	x.SetDisplayName("IZU 控制单元");
//	x.SetDescription("");
//#if true
//	x.EnableServiceRecovery(r =>
//	{
//		if (recoverySeconds > 0.1)
//			r.RestartService(TimeSpan.FromSeconds(recoverySeconds));

//		//操作限制在2-3个
//		//r.RestartComputer(5, "message");
//		//r.RestartComputer(1,"restart computer");
//		//r.RunProgram(7, "ping www.baidu.com");
//		//r.OnCrashOnly();
//		//r.SetResetPeriod(1);
//	});
//#endif
//	x.OnException(ex =>
//	{
//		LogOnce($"{AppDomain.CurrentDomain.BaseDirectory}logs\\{DateTime.Now:yyyyMMddHHmmss}-crash.log", ex.StackTrace);
//	});
//	x.StartAutomatically();
//	x.RunAsNetworkService();
//	//x.RunAs("administrator", "duan");
//	//x.RunAsPrompt();
//});
//TopshelfExitCode rc = host.Run();
//var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());//11
//Environment.ExitCode = exitCode;
