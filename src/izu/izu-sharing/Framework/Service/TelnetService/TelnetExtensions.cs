using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;

namespace Wonder.Service
{
    /// <summary>
    /// 扩展方法使用顺序（注：顺序不能颠倒，因为日志在一开始就需要初始化）
    /// 1. 在CreateBuilder后添加AddTelnetLogger（该方法会初始化TelnetLogger）
    /// 2. 然后在添加服务 AddTelnetService 
    /// 3. 在var app = builder.Build() 后添加 UseTelnet
    /// </summary>
    public static class TelnetExtensions
    {
        /// <summary>
        /// 添加自定义日志扩展方法
        /// 用于远程访问服务时，将日志输出到 Telnet 客户端
        /// </summary>
        /// <param name="builder"><see cref="ILoggingBuilder"/></param>
        /// <param name="configure"><see cref="TelnetLoggerConfiguration"/></param>
        /// <returns></returns>
        public static ILoggingBuilder AddTelnetLogger(this ILoggingBuilder builder, Action<TelnetLoggerConfiguration> configure)
        {
            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TelnetLoggerProvider>());
            LoggerProviderOptions.RegisterProviderOptions<TelnetLoggerConfiguration, TelnetLoggerProvider>(builder.Services);
            builder.Services.Configure(configure);
            return builder;
        }
        /// <summary>
        /// 添加 Telnet 服务器
        /// </summary>
        /// <param name="service"><see cref="IServiceCollection"/></param>
        /// <returns></returns>
        public static IServiceCollection AddTelnetService(this IServiceCollection service)
        {
            service.AddSingleton<ITelnetService, WonderTelnetService>();
            return service;
        }
        /// <summary>
        /// 启动 Telnet 服务器
        /// </summary>
        /// <param name="app"></param>
        /// <exception cref="Exception"></exception>
        public static void UseTelnet(this WebApplication app)
        {
            ITelnetService? telnetService = app.Services.GetService<ITelnetService>();
            if (telnetService == null)
                throw new Exception("should add TelnetService first");
            WonderTelnetService.TelnetService = telnetService;
            telnetService.Start();
        }
    }
}
