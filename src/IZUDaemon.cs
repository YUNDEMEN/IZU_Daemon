using IZU.Entities;
using IZU.Interfaces;
using IZU.Service;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
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
				builder.Services.AddControllers();
				builder.Services.Configure<IZUConfig>(builder.Configuration.GetSection(IZUConfig.KEY));
				builder.Services.AddSingleton<IIZUService, IZUService>();
				builder.Services.AddSingleton<IDataPoolService, DataPoolService>();
				var app = builder.Build();
				serviceInstance = app.Services.GetService<IIZUService>();
				serviceInstance?.Start();
				app.MapGet("/", () => serviceInstance?.ServiceRuntime);
				//app.UseAuthorization();
				app.MapControllers();
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
