
using izu.quartz;
using NLog.Extensions.Logging;


var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory
};

/*
 遗留问题：
1. nlog 日志，注入 Microsoft.logging
2. 健康检查
 */

var builder = WebApplication.CreateBuilder(options);
builder.Logging.ClearProviders();
builder.Logging.AddNLog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nlog.config"));
builder.Host.UseWindowsService();
builder.Services.AddQuartz(builder.Configuration); 
builder.Services.AddHealthChecks();
var app = builder.Build();
app.MapGet("/", () => $"Wonder Service!");
app.MapHealthChecks("/check");
app.Run();
