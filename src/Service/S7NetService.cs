using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using TinyCsvParser;

namespace IZU.Service
{
    public class S7NetService : IS7NetService
    {
        private readonly ConcurrentDictionary<string, DeviceEntity> _cDic = new();
        private IZUConfig _config { get; set; }
        private readonly ILogger<S7NetService> _logger;
        //private readonly IIZUService _izuService;

        public S7NetService(ILogger<S7NetService> logger, IOptions<IZUConfig> cfg)
        {
            _logger = logger;
            _config = cfg.Value;
        }
        public async Task StartAsync()
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
            foreach (var deviceFile in files)
            {
                List<VariableEntity> variables = new();
                CsvParserOptions csvParserOptions = new(true, ',');
                CsvVariableMapping csvMapper = new();
                CsvParser<VariableEntity> csvParser = new(csvParserOptions, csvMapper);
                try
                {
                    var csvs = csvParser.ReadFromFile(deviceFile.FullName, Encoding.ASCII);
                    foreach (var item in csvs)
                    {
                        if (!item.IsValid || item.Error != null)
                        {
                            _logger.LogWarning("load device table {0} error, {1}", deviceFile, item.Error);
                            continue;
                        }
                        variables.Add(item.Result);
                        _logger.LogDebug("device table loaded {0}, variable count {1}", deviceFile, variables.Count);
                    }
                    var groups = variables.GroupBy(t => t.DeviceName, t => t);
                    foreach (var item in groups)
                    {
                        if (string.IsNullOrEmpty(item.Key)) continue;
                        if (!_cDic.TryAdd(item.Key.ToLower(), new DeviceEntity(deviceFile.FullName, item.Key, _config.RefreshMillionSeconds, item.ToList())))
                            _logger.LogWarning("add device failed, device name: {0}   file: {1}", item.Key, deviceFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("load device table {0} exception, {1}", deviceFile, ex.Message);
                    throw;
                }
            }

            _logger.LogInformation("end loading device table");
            await Task.Delay(10);
        }
        public void Stop()
        {
            foreach (var deviceEntity in _cDic.Values.ToList())
            {
                deviceEntity.Dispose();
            }
            _cDic.Clear();
        }

        public List<string> GetAllDeviceNames()
        {
            return _cDic.Keys.ToList();
        }
        public List<DeviceEntity> GetAllDevices()
        {
            return _cDic.Values.ToList();
        }

        public DeviceEntity? GetDevice(string deviceName)
        {
            _ = _cDic.TryGetValue(deviceName.ToLower(), out var device);
            return device;
        }

        public List<VariableEntity> GetDeviceVariables(string deviceName)
        {
            DeviceEntity? device = GetDevice(deviceName.ToLower());
            if (device == null) return new List<VariableEntity>();
            return device.Variables;
        }

        public void RefreshConfig(IZUConfig config)
        {
            _config = config;
            foreach (var deviceEntity in GetAllDevices())
            {
                deviceEntity.Refresh(config.RefreshMillionSeconds);
            }
        }

    }
}
