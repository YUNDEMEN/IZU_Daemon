using IZU.Interfaces;

namespace IZU
{
    public class IZUDaemon
	{
		private IIZUService? serviceInstance;
		public IZUDaemon()
		{
		}

		public bool Start()
		{
#if false
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
#endif
			return true;
		}

		public bool Stop()
		{
			serviceInstance?.Stop();
			return true;
		}
	}
}
