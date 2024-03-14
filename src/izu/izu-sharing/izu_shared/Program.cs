using IZU.Base;
using NLog.Extensions.Logging;
using System.Text;
using Wonder.Infrastructure;
using Wonder.Service;
using Wonder.Service.Framework;

#region 检查程序配置是否存在
DirectoryInfo dir = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startEx"));
if (dir.Exists) dir.Delete(true);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
void StartInfo(string fileName, string? content)
{
    if (!dir.Exists) dir.Create();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}]: {1}", DateTime.Now, content);
    File.AppendAllText(Path.Combine(dir.FullName, fileName), content);
    Console.ForegroundColor = ConsoleColor.White;
}

AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
{
    StartInfo($"{AppDomain.CurrentDomain.BaseDirectory}logs\\{DateTime.Now:yyyyMMddHHmmss}-crash.log", e.ExceptionObject?.ToString());
};

string result = IZUConfig.Read();
if (!string.IsNullOrEmpty(result))
    StartInfo($"startinfo.log", result);

if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config")))
{
    StartInfo($"startinfo.log", "NLog config file missing (nlog.config)");
    return;
}

#endregion

var opt = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
    WebRootPath = AppDomain.CurrentDomain.BaseDirectory
};

try
{
    int index = Array.IndexOf(opt.Args, "--urls");
    if (index < 0)
        throw new Exception("未设置Url");
    if (opt.Args.Length > index + 1)
    {
        var url = new Uri(opt.Args[index + 1]);
        IZUConfig.ServerIP = url.Host;
        IZUConfig.ServerPort = url.Port;
    }
}
catch (Exception ex)
{
    StartInfo($"startinfo.log", $"服务IP设置不正确: {ex.Message}");
    return;
}

var builder = WebApplication.CreateBuilder(opt);
builder.Logging.ClearProviders();
builder.Configuration.AddJsonFile("appsettings.json", false, true);
builder.Logging.AddNLog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config"));
builder.Logging.AddTelnetLogger(configuration =>
{
    var c = builder.Configuration.GetRequiredSection("Logging:ColorConsole:LogLevelToColorMap").GetChildren();
    //Replace LogLevel and ConsoleColor values from appsettings.json
    configuration.LogLevelToColorMap = c.ToDictionary(
        t => (LogLevel)Enum.Parse(typeof(LogLevel), t.Key),
        v => (ConsoleColor)Enum.Parse(typeof(ConsoleColor), v.Value!)
        );
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

//builder.Services.BuildServiceProvider()
//.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<IZUConfig>>()
//.OnChange((profile,t) =>
//{
//});

var app = builder.Build();
app.UseTelnet();
//app.UseAuthorization();
app.UseCors("AllowAnyOrigin");
app.UseWebSockets();
app.MapControllers();

//app.Map("/allservices", async (context) =>
//{
//    var sb = new StringBuilder();
//    sb.Append("<h1>All Services</h1>");
//    sb.Append("<table><thead>");
//    sb.Append("<tr><th>Type</th><th>Lifetime</th><th>Instance</th></tr>");
//    sb.Append("</thead><tbody>");
//    foreach (var svc in _services)
//    {
//        sb.Append("<tr>");
//        sb.Append($"<td>{svc.ServiceType.FullName}</td>");
//        sb.Append($"<td>{svc.Lifetime}</td>");
//        sb.Append($"<td>{svc.ImplementationType?.FullName}</td>");
//        sb.Append("</tr>");
//    }
//    sb.Append("</tbody></table>");
//    await context.Response.WriteAsync(sb.ToString());
//});

await app.RunAsync();