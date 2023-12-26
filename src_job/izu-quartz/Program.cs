
using izu.quartz;
using NLog.Extensions.Logging;
using System.Reflection;
/*
            //设置内存配置并应用
            var rule = NLog.LogManager.Configuration.FindRuleByName("console");
            rule.SetLoggingLevels(NLog.LogLevel.Info, NLog.LogLevel.Info);
            NLog.LogManager.ReconfigExistingLoggers();

            //重新加载本地配置并应用
            NLog.LogManager.Configuration = NLog.LogManager.Configuration.Reload();
            NLog.LogManager.ReconfigExistingLoggers();
 */

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
    WebRootPath = AppDomain.CurrentDomain.BaseDirectory
};

Directory.SetCurrentDirectory(options.ContentRootPath);
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
