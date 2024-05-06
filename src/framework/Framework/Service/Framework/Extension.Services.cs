using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Wonder.Infrastructure;

namespace Wonder.Service.Framework
{
    public static class FrameworkExtensions
    {
        public static IServiceCollection RegistServices(this IServiceCollection services, IConfiguration configuration)
        {
            var fact = LogManager.ConfigureLogger(services.BuildServiceProvider().GetRequiredService<ILoggerFactory>());
            var logger = fact.CreateLogger("wonder.service.framework");

            DirectoryInfo root = new(AppDomain.CurrentDomain.BaseDirectory);
            var modFiles = root.GetFiles("izu*mod.dll", SearchOption.TopDirectoryOnly);
            foreach (var modFile in modFiles)
            {
                try
                {
                    var mod = AppDomain.CurrentDomain.Load(File.ReadAllBytes($"{AppDomain.CurrentDomain.BaseDirectory}izu.config.mod.dll"));
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"load mod error: {ex.Message}. {modFile.FullName}");
                    logger.LogError($"load mod error: {ex.StackTrace}. {modFile.FullName}");
                }
            }

            Type registration = typeof(RegistAttribute);
            var AddHostedService = typeof(ServiceCollectionHostedServiceExtensions)
                .GetMethod("AddHostedService", 1, BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(IServiceCollection) }, null);

            var domainTypies = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes());
            var registTypies = domainTypies.Where(t => t.IsAssignableFrom(t) && t.IsDefined(registration, true) && !t.IsInterface).Select(t => new TypeService(t.Name, null, t));
            var specTypies = registTypies.Select(t => new TypeService(t.Key, t.Implementation.GetInterface($"I{t.Key}"), t.Implementation)).Where(t => t.Service != null);
            /*
               注册拥有具体接口的服务：
                    MyService : IMyService
               注： 服务的接口命名规则为  I（大写的i） + 服务类称
             */
            foreach (var type in specTypies)
            {
                var regist = type.Implementation.GetCustomAttribute<RegistAttribute>();
                if (regist == null) continue;

                if (regist.IsScoped)
                {
                    services.AddScoped(type.Service!, type.Implementation);
                }
                else if (regist.IsSingleton)
                {
                    services.AddSingleton(type.Service!, type.Implementation);
                }
                else if (regist.IsTransient)
                {
                    services.AddTransient(type.Service!, type.Implementation);
                }
            }

            foreach (var type in registTypies)
            {
                var regist = type.Implementation.GetCustomAttribute<RegistAttribute>();
                if (regist == null) continue;
                if (regist.IsLongRunningTask)
                {
                    var longRuningTaskInterface = type.Implementation.GetInterface(nameof(ILongRunningTask));
                    if (longRuningTaskInterface == null) break;
                    var serviceInterface = type.Implementation.GetInterface($"I{type.Implementation.Name}");

                    // 注册 Singleton Service
                    if (regist.IsSingleton)
                    {
                        if (serviceInterface == null)
                            logger.LogDebug($"Fail to Register {longRuningTaskInterface} -> I{type.Implementation.Name} is not found.");
                        else
                        {
                            services.AddSingleton(longRuningTaskInterface, t => t.GetRequiredService(serviceInterface));
                            logger.LogDebug($"Register {longRuningTaskInterface} -> {serviceInterface}");
                        }
                    }
                    else
                    {
                        services.AddSingleton(longRuningTaskInterface, type.Implementation);
                        logger.LogDebug($"Register {longRuningTaskInterface} -> I{type.Implementation.Name}");
                    }
                    //services.AddKeyedSingleton(longRuningTaskInterface, type.Implementation.Name, type.Implementation);
                }
                // 注册 Hosted Service
                else if (regist.IsHostedService)
                {
                    if (AddHostedService == null)
                        throw new NullReferenceException("Main Hosted Service Can not Be NULL");
                    AddHostedService.MakeGenericMethod(new Type[] { type.Implementation }).Invoke(null, new object?[] { services });
                }
            }
            return services;
        }
    }
}
