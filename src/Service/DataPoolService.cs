using IZU.Entities;
using IZU.Interfaces;
using NLog.LayoutRenderers;
using System.Collections.Concurrent;
using System.Text;
using TinyCsvParser;

namespace IZU.Service
{
    public class DataPoolService : IDataPoolService
    {
        private readonly ConcurrentDictionary<string, Device> _cDic = new ConcurrentDictionary<string, Device>();
        private readonly IIZUConfigService? _configService;
        private readonly ILogger<DataPoolService> _logger;
        private readonly IIZUService _izuService;
        private DataPoolService? samplePool;

        public DataPoolService(ILogger<DataPoolService> logger, IIZUConfigService configService)
        {
            _logger = logger;
            _configService = configService;
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

        public IDataPoolService Samples
        {
            get
            {
                if (samplePool != null) return samplePool;
                string sampleFile = _configService.Config.SampleFile;
                List<Variable> devices = new List<Variable>();
                if (File.Exists(sampleFile))
                {
                    CsvParserOptions csvParserOptions = new CsvParserOptions(true, ',');
                    CsvVariableMapping csvMapper = new CsvVariableMapping();
                    CsvParser<Variable> csvParser = new CsvParser<Variable>(csvParserOptions, csvMapper);
                    devices = csvParser
                                 .ReadFromFile(sampleFile, Encoding.ASCII)
                                 .Where(t => t.IsValid && t.Error == null)
                                 .Select(t => t.Result)
                                 .ToList();
                }
                else
                {
                    _logger.LogInformation($"sample file {sampleFile} doesn't exist");
                }
                samplePool = new(_logger, _configService);
                samplePool.TryAdd(new("PSP", devices));
                return samplePool;
            }
        }
    }
}
