using IZU.Controllers;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.NetworkInformation;
using System.Xml.Linq;

namespace IZU.Service
{
    public class IZUService : IIZUService
    {
        public ServiceRuntime ServiceRuntime { get; }
        private readonly ILogger<IZUService> _logger;
        private readonly Timer _timer;
        private readonly IZUConfig _config;
		private IDataPoolService _dataPool;

        public IZUService(ILogger<IZUService> logger, IOptions<IZUConfig> cfg, IDataPoolService dataPool)
		{
			_logger = logger;
            _config = cfg.Value;
			_dataPool = dataPool;
			_timer = new Timer(Callback);
			ServiceRuntime = new ServiceRuntime
            {
                Name= _config.Name,
				IP = _config.Server
            };
			logger.LogInformation("IZU Service Initialized");
		}
		public void Start()
		{
			_logger.LogInformation("---------------IZU Service Begin Start---------------");
			_timer.Change(1000, 1000);
			_dataPool.LoadDevices();
			_logger.LogInformation("---------------IZU Service End Start---------------");
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

        public List<Device> GetSampleDevices()
        {
            return _dataPool.Samples;
        }
        public List<Device> GetDevices()
        {
            return _dataPool.GetAllDevices();
        }
		public Device? GetDevice(string name)
		{
			return _dataPool.GetDevice(name);
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