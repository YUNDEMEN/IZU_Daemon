using IZU.Controllers;
using IZU.Entities;
using IZU.Interfaces;
using System.Net;
using System.Net.NetworkInformation;
using System.Xml.Linq;

namespace IZU.Service
{
    public class IZUService : IIZUService
    {
        public ServiceRuntime ServiceRuntime { get; }
        private readonly ILogger<IZUService> _logger;
        private readonly IIZUConfigService? _configService;
        private readonly Timer _timer;
        private IDataPoolService _dataPool;

        public IZUService(ILogger<IZUService> logger, IIZUConfigService configService, IDataPoolService dataPool)
		{
			_logger = logger;
			_configService = configService;
			_dataPool = dataPool;

			_timer = new Timer(Callback);
			ServiceRuntime = new ServiceRuntime
            {
                Name= configService.Config.Name,
				IP = configService.Config.Server
            };
            Start();
			logger.LogInformation("IZUService Initialized");
		}

        void Callback(object? state)
        {
            Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
            Console.Write("★★★★★ IZU Service is running! [{0:yyyy-MM-dd HH:mm:ss:fff}] ★★★★★★★", DateTime.Now);
        }

        public void Start()
        {
            _timer.Change(1000, 1000);
        }

        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public List<Device> GetSampleDevices()
        {
            return _dataPool.Samples.GetAllDevices();
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