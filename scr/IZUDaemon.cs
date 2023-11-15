using NLog.Extensions.Logging;
using Topshelf;

namespace IZU
{
	public class IZUDaemon : ServiceControl
	{
		private IIZUService? serviceInstance;
		public IZUDaemon()
		{
			Task.Factory.StartNew(() => {
				var builder = WebApplication.CreateBuilder();
				builder.Logging.AddNLog("nlog.config");
				builder.Services.AddControllers();
				IServiceCollection sc = builder.Services.AddSingleton<IIZUService, IZUService>();
				var app = builder.Build();
				serviceInstance = app.Services.GetService<IIZUService>();
				Console.WriteLine(serviceInstance?.Name);
				app.MapGet("/", () => serviceInstance?.Name);
				//app.UseAuthorization();
				app.MapControllers();
				app.Run();
			});
		}
		public bool Start(HostControl hostControl)
		{
			serviceInstance?.Start();
			return true;
		}

		public bool Stop(HostControl hostControl)
		{
			serviceInstance?.Stop();
			return true;
		}
	}
}
