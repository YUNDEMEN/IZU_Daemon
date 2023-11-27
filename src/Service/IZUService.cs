using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.Extensions.Options;

namespace IZU.Service
{
	public class IZUService : IIZUService
    {
        public ServiceRuntime ServiceRuntime { get; }
        private readonly ILogger<IZUService> _logger;
        private readonly Timer _timer;
        private readonly IZUConfig _config;
		private IS7NetService _s7netService;

        public IZUService(ILogger<IZUService> logger, IOptions<IZUConfig> cfg, IS7NetService s7netService)
		{
			_logger = logger;
            _config = cfg.Value;
			_s7netService = s7netService;
			_timer = new Timer(Callback);
			ServiceRuntime = new ServiceRuntime
            {
                Name= _config.Name,
				IP = _config.Server,
				RecoverySeconds = $"{_config.RecoverySeconds} s",
                RefreshInterval = $"{_config.RefreshMillionSeconds.ToString()} ms"
            };
			logger.LogInformation("IZU service initialized");
		}
		public async Task StartAsync()
		{
			_logger.LogInformation("---------------IZU service starting---------------");
			_timer.Change(1000, 1000);

            await _s7netService.StartAsync();

			//ServiceRuntime.

			_logger.LogInformation("---------------IZU service started---------------");
		}

		void Callback(object? state)
        {
            //Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
            //Console.Write("★★★★★ IZU Service is running! [{0:yyyy-MM-dd HH:mm:ss:fff}] ★★★★★★★", DateTime.Now);
        }


        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

	}
}
/*
    获取本地系统 IP 
    如果是多网卡，则需要指定网卡
    //IPHostEntry ipEntry = Dns.GetHostEntry(Dns.GetHostName(), System.Net.Sockets.AddressFamily.InterNetwork);
    //IPAddress[] addr = ipEntry.AddressList;
    //string ip = addr.FirstOrDefault()?.ToString() 
 */