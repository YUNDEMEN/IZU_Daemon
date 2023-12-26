using IZU.Base;
using IZU.Entities;
using Newtonsoft.Json.Linq;
using NLog.Extensions.Logging;

#region 检查程序配置是否存在
DirectoryInfo dir = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startEx"));
if (dir.Exists) dir.Delete(true);
dir.Create();

void StartInfo(string fileName, string? content)
{
    if (!dir.Exists) dir.Create();
    Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss}]: {1}", DateTime.Now, content);
    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName), content);
}

int recoverySeconds = 0;
AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
{
    StartInfo($"{AppDomain.CurrentDomain.BaseDirectory}logs\\{DateTime.Now:yyyyMMddHHmmss}-crash.log", e.ExceptionObject?.ToString());
};

string appsettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
if (!File.Exists(appsettingsPath))
{
    StartInfo($"startinfo.log", $"config file [{appsettingsPath}] missing!");
    return;
}

string json = File.ReadAllText(appsettingsPath);
try
{
    JObject configJson = JObject.Parse(json);
    var izuNode = configJson["izu"];
    if (izuNode == null)
    {
        StartInfo($"startinfo.log", "service node not found!");
        return;
    }
    var recoverySecondsNode = izuNode["recoverySeconds"];
    if (recoverySecondsNode == null)
    {
        StartInfo($"startinfo.log", "recoverySecondsNode not found!");
        return;
    }
    recoverySeconds = recoverySecondsNode.Value<int>();
}
catch (Exception ex)
{
    StartInfo($"startinfo.log", ex.Message + ex.StackTrace);
    return;
}
if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config")))
{
    StartInfo($"startinfo.log", "NLog config file missing (nlog.config)");
    return;
}

#endregion

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
    WebRootPath = AppDomain.CurrentDomain.BaseDirectory
});
builder.Logging.ClearProviders();
builder.Logging.AddNLog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config"));
builder.Host.UseWindowsService();
builder.Host.ConfigureServices(s =>
{
    s.AddCors(options =>
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
});
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
builder.Services.AddIZU(builder.Configuration.GetSection(IZUConfig.KEY));

//builder.Services.BuildServiceProvider()
//.GetRequiredService<IOptionsMonitor<IZUConfig>>()
//.OnChange((profile) =>
//{

//});

var app = builder.Build();
//app.UseAuthorization();
app.UseCors("AllowAnyOrigin");
app.MapControllers();
await app.UseIZUAsync();
await app.RunAsync();