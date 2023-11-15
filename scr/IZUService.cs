using IZU.Controllers;
using System.Xml.Linq;

namespace IZU
{
	public class IZUService : IIZUService
	{
		public string Name { get; }
		readonly Timer _timer;
		private readonly ILogger<IZUService> _logger;
		int _counter = 0;

		public IZUService(ILogger<IZUService> logger)
		{
			Name = $"IZU SERVICE {Guid.NewGuid()}";
			_logger = logger;
			_logger.LogInformation("IZUService Initialized");
			_timer = new Timer(Callback);
			Start();
		}

		void Callback(object? state)
		{
			if (_counter > 5)
			{
				//throw new Exception("this is an error");
			}
			Console.WriteLine("[{0:yyyy-MM-dd HH:mm:ss:fff}] service running", DateTime.Now);
			_counter++;
		}

		public void Start()
		{
			_counter = 1;
			_timer.Change(0, 1000);
		}

		public void Stop()
		{
			_timer.Change(Timeout.Infinite, Timeout.Infinite);
		}

	}
}
