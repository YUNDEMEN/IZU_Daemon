using IZU.Entities;
using IZU.Interfaces;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using TinyCsvParser;

namespace IZU.Service
{
	public class DataPoolService : IDataPoolService
    {
        private readonly ConcurrentDictionary<string, Device> _cDic = new ConcurrentDictionary<string, Device>();
		private readonly IZUConfig _config;
		private readonly ILogger<DataPoolService> _logger;
        //private readonly IIZUService _izuService;

        public DataPoolService(ILogger<DataPoolService> logger, IOptions<IZUConfig> cfg)
        {
            _logger = logger;
			_config = cfg.Value;
        }

        public void LoadDevices()
		{
			_logger.LogInformation("start loading device table");
			_cDic.Clear();

            DirectoryInfo dir = new(_config.DeviceFiles);
			if (!dir.Exists)
			{
				_logger.LogWarning("device table missing");
				return;
			}

			var files = dir.GetFiles("*.csv");
			List<Variable> variables = new List<Variable>();
			foreach (var deviceFile in files)
			{
				CsvParserOptions csvParserOptions = new CsvParserOptions(true, ',');
				CsvVariableMapping csvMapper = new CsvVariableMapping();
				CsvParser<Variable> csvParser = new CsvParser<Variable>(csvParserOptions, csvMapper);
				variables = csvParser
							 .ReadFromFile(deviceFile.FullName, Encoding.ASCII)
							 .Where(t => t.IsValid && t.Error == null)
							 .Select(t => t.Result)
							 .ToList();
				var groups = variables.GroupBy(t => t.DeviceName, t => t);
				foreach (var item in groups)
				{
					if (item.Key == null) continue;
					TryAdd(new Device(item.Key, item.ToList()));
				}
			}

			_logger.LogInformation("end loading device table");
		}

        public bool TryAdd(Device value)
        {
            if (value == null) return false;
            return _cDic.TryAdd(value.Name.ToLower(), value);
        }

        public List<string> GetAllDeviceNames()
        {
            return _cDic.Keys.ToList();
        }
        public List<Device> GetAllDevices()
        {
            return _cDic.Values.ToList();
        }

        public Device? GetDevice(string deviceName)
        {
            _ = _cDic.TryGetValue(deviceName.ToLower(), out var device);
            return device;
        }

        public List<Variable> GetDeviceVariables(string deviceName)
        {
            Device? device = GetDevice(deviceName.ToLower());
            if (device == null) return new List<Variable>();
            return device.Variables;
        }

		public List<Device> Samples
        {
            get
			{
				List<Device> devices = new();
				DirectoryInfo dir = new(_config.SampleFiles);
				if (!dir.Exists) return devices;
                var files = dir.GetFiles("*.csv");
				List<Variable> variables = new List<Variable>();
				foreach (var sampleFile in files)
				{
					CsvParserOptions csvParserOptions = new CsvParserOptions(true, ',');
					CsvVariableMapping csvMapper = new CsvVariableMapping();
					CsvParser<Variable> csvParser = new CsvParser<Variable>(csvParserOptions, csvMapper);
					variables = csvParser
								 .ReadFromFile(sampleFile.FullName, Encoding.ASCII)
								 .Where(t => t.IsValid && t.Error == null)
								 .Select(t => t.Result)
								 .ToList();
					var groups = variables.GroupBy(t => t.DeviceName, t => t);
					foreach (var item in groups)
					{
						devices.Add(new Device(item.Key, item.ToList()));
					}
				}
                if (devices.Count == 0)
                    _logger.LogInformation($"sample folder path {dir.FullName} doesn't exist");
				return devices;
            }
        }
    }
}
