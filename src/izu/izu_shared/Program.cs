using IZU.Base;
using IZU.Interfaces;
using IZU.Tasks;
using NLog.Extensions.Logging;
using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text;
using Wonder.Infrastructure;
using Wonder.Service;
using Wonder.Service.Framework;

#region 检查程序配置是否存在
DirectoryInfo dir = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startEx"));
if (dir.Exists) dir.Delete(true);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
void ErrorReport(string fileName, string? content)
{
    if (!dir.Exists) dir.Create();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}]: {1}", DateTime.Now, content);
    File.AppendAllText(Path.Combine(dir.FullName, fileName), content);
    Console.ForegroundColor = ConsoleColor.White;

#if Linux
        // linux action
#elif OSX
        // mac os action
#elif Windows
        //这段代码在Linux运行中报错，所以要在windows环境下才执行
        //Console.WriteLine("按任意键继续...");
        //Console.ReadKey();
#endif
    Environment.Exit(0);
}

AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
{
    ErrorReport($"{AppDomain.CurrentDomain.BaseDirectory}logs\\{DateTime.Now:yyyyMMddHHmmss}-crash.log", e.ExceptionObject?.ToString());
};

string result = IZUConfig.Read();
if (!string.IsNullOrEmpty(result))
    ErrorReport($"startinfo.log", result);

if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config")))
{
    ErrorReport($"startinfo.log", "NLog config file missing (nlog.config)");
}

#endregion

var opt = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
    WebRootPath = AppDomain.CurrentDomain.BaseDirectory
};


string error = string.Empty;

if (opt.Args.Count() < 2)
{
    try
    {
        var addressList = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList;
        var ip = addressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();
        if (string.IsNullOrEmpty(ip))
            throw new NullReferenceException("获取本机IP失败");
        Console.WriteLine(ip);
        IZUConfig.ServerIP = ip;
        IZUConfig.ServerPort = 8031;
    }
    catch (Exception ex)
    {
        error += "get local ipaddress failed: " + ex.StackTrace;
    }
}
else
{
    Uri? serverUrl = null;
    foreach (var item in opt.Args)
    {
        string addr = item.Replace("--urls=", string.Empty);
        try
        {
            serverUrl = new Uri(addr);

            IZUConfig.ServerIP = serverUrl.Host;
            IZUConfig.ServerPort = serverUrl.Port;
            break;
        }
        catch (Exception ex)
        {
            error += addr + ex.StackTrace;
        }
    }
    if (serverUrl == null)
    {
        ErrorReport($"startinfo.log", $" {{urls}} should be passed in");
        throw new Exception("未设置本地IP地址和端口");
    }
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var builder = WebApplication.CreateBuilder(opt);
builder.WebHost.UseUrls($"http://{IZUConfig.ServerIP}:{IZUConfig.ServerPort}");
builder.Logging.ClearProviders();
builder.Configuration.AddJsonFile("appsettings.json", false, true);
builder.Logging.AddNLog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config"));
builder.Logging.AddTelnetLog(configuration =>
{
    /*     
     *     Telnet Log 日志颜色配置
  
          "Logging": {
            "LogLevel": {
              "Default": "Debug",
              "Microsoft.AspNetCore": "Warning"
            },
            "ColorConsole": {
              "LogLevelToColorMap": {
                "Debug": "Gray",
                "Information": "Green",
                "Warning": "Yellow",
                "Error": "Magenta",
                "Critical": "Red"
              }
            }
          },
     */
    try
    {
        var section = builder.Configuration.GetRequiredSection("Logging:ColorConsole:LogLevelToColorMap");
        var colorSettings = section.GetChildren();
        //Replace LogLevel and ConsoleColor values from appsettings.json
        configuration.LogLevelToColorMap = colorSettings.ToDictionary(
                t => (LogLevel)Enum.Parse(typeof(LogLevel), t.Key),
                v => (ConsoleColor)Enum.Parse(typeof(ConsoleColor), v.Value!)
            );
    }
    catch
    {
        Console.WriteLine("telnet log settings is not available!");
    }
});
builder.Host.UseWindowsService();
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
builder.Services.AddTelnetService();
builder.Services.RegistServices(builder.Configuration);
builder.Services.AddSingleton<CommandServer>();
//builder.Services.BuildServiceProvider()
//.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<IZUConfig>>()
//.OnChange((profile,t) =>
//{
//});

var app = builder.Build();
//程序启动时，可以捕捉绑定的Url列表
//app.Lifetime.ApplicationStarted.Register(() =>
//{
//    ICollection<string> urls = app.Urls;
//    var serverUrl = new Uri(urls.First());
//    IZUConfig.ServerIP = serverUrl.Host;
//    IZUConfig.ServerPort = serverUrl.Port;
//});
app.UseTelnet();
app.UseCors("AllowAnyOrigin");
app.UseWebSockets();
app.MapControllers();

app.Map("/", async (context) =>
{
    var specServices = builder.Services.Where(t =>
    !string.IsNullOrEmpty(t.ServiceType.FullName) &&
    !t.ServiceType.FullName.StartsWith("Microsoft") &&
    !t.ServiceType.FullName.StartsWith("System"))
    .Select(t => t);
    var block = new StringBuilder();
    block.Append("<head><meta charset=\"utf-8\"></meta></head>");
    block.Append("<style>td.left { text-align: left; vertical-align: middle;}");
    block.Append("h1 { text-align: center; vertical-align: middle;}");
    block.Append("th.left { text-align: left; vertical-align: middle;}</style>");
    block.Append("<h1>INTELLIGENT ZONE UNIT BACKEND SERVICE</h1>");
    block.Append("<div style=\"display: flex;width: 100%;\">");
    block.Append("<ul style=\"flex: 0 0 20%;\">");
    block.Append("<h2>Registered Services</h2>");
    block.Append("<table><thead>");
    block.Append("<tr><th class=\"left\">Name</th><th class=\"left\">Lifetime</th></tr>");
    block.Append("</thead><tbody>");
    foreach (var svc in specServices)
    {
        block.Append("<tr>");
        if (svc.ImplementationType == null)
        {
            continue;
        }
        block.Append($"<td width=\"40%\" class=\"left\">{svc.ServiceType.Name}<br>&#x21aa {svc.ImplementationType.Name}</td>");
        block.Append($"<td width=\"40%\">{svc.Lifetime}</td>");
        block.Append("</tr>");
    }
    block.Append("</tbody></table>");
    block.Append("</ul>");

    block.Append("<ul style=\"flex: 0 0 70%;\">");
    block.Append("<h2>Start Info</h2>");
    block.Append("<table><thead>");
    block.Append("<tr><th class=\"left\"></th></tr>");
    block.Append("</thead><tbody>");

    var sp = context.RequestServices;
    var izus = sp.GetService<IIZUService>();
    if (izus != null)
    {
        foreach (var log in izus.Logs)
        {
            block.Append("<tr>");
            block.Append($"<td width=\"40%\">{log.Replace("\r\n", "<br>")}</td>");
            block.Append("</tr>");
        }
    }

    block.Append("</tbody></table>");
    block.Append("</ul>");
    block.Append("</div>");
    await context.Response.WriteAsync(block.ToString());
});
await app.RunAsync();