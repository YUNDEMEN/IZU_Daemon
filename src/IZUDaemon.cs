using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using IZU.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using Topshelf;

namespace IZU
{
	public class IZUDaemon : ServiceControl
	{
		private IIZUService? serviceInstance;
		public IZUDaemon()
		{
		}

		public bool Start(HostControl hostControl)
		{
			Task webhostTask = Task.Factory.StartNew(() =>
			{
				var builder = WebApplication.CreateBuilder();
				if (!File.Exists("nlog.config"))
				{
					Console.WriteLine("NLog config file missing (nlog.config)");
					throw new FileNotFoundException("NLog config file missing (nlog.config)");
				}
				builder.Logging.AddNLog("nlog.config");
				builder.Services.AddControllers().AddJsonOptions(options =>
				{
					options.JsonSerializerOptions.Converters.Add(new DatetimeJsonConverter("yyyy-MM-dd HH:mm:ss"));
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
				serviceInstance = app.Services.GetService<IIZUService>();
				serviceInstance?.StartAsync();
				app.MapGet("/", () => serviceInstance?.ServiceRuntime);
				//app.UseAuthorization();
				app.MapControllers();
				app.UseBroadcastServer(serviceInstance);
				app.Run();
			});
			return true;
		}

		public bool Stop(HostControl hostControl)
		{
			serviceInstance?.Stop();
			return true;
		}
	}
}
