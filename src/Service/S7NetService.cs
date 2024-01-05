using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Text.Json.Nodes;
using TinyCsvParser;

namespace IZU.Service
{
    public class S7NetService : IS7NetService
    {
        private readonly ConcurrentDictionary<string, DeviceEntity> _cDic = new();
        private readonly ILogger<S7NetService> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public S7NetService(ILoggerFactory loggerFactory, ILogger<S7NetService> logger)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
        }
        public async Task StartAsync()
        {
            string devicefile;
            List<VariableEntity> variables = new List<VariableEntity>();
            if (IZUConfig.DeviceTableFrom == "db")
            {
                devicefile = "db";
                 variables = await GetDeviceTableFromDBAsync();
            }
            else
            {
                devicefile = "csv";
                variables = await GetDeviceTableFromLocalFileAsync();
            }
            var groups = variables.GroupBy(t => t.DeviceName, t => t);
            foreach (var item in groups)
            {
                if (string.IsNullOrEmpty(item.Key)) continue;
                if (!_cDic.TryAdd(item.Key.ToLower(), new DeviceEntity(_loggerFactory, devicefile, item.Key, item.ToList())))
                    _logger.LogWarning("add device failed, device name: {0}   file: {1}", item.Key, devicefile);
            }
        }

        async Task<List<VariableEntity>> GetDeviceTableFromLocalFileAsync()
        {
            _logger.LogInformation("start loading device table");
            _cDic.Clear();
            DirectoryInfo dir = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeviceTable"));
            if (!dir.Exists)
            {
                _logger.LogWarning("device table missing");
                return new List<VariableEntity>();
            }
            List<VariableEntity> variables = new List<VariableEntity>();
            var files = dir.GetFiles("*.csv");
            foreach (var deviceFile in files)
            {
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
                    //var groups = variables.GroupBy(t => t.DeviceName, t => t);
                    //foreach (var item in groups)
                    //{
                    //    if (string.IsNullOrEmpty(item.Key)) continue;
                    //    if (!_cDic.TryAdd(item.Key.ToLower(), new DeviceEntity(_loggerFactory, deviceFile.FullName, item.Key, IZUConfig.RefreshMillionSeconds, item.ToList())))
                    //        _logger.LogWarning("add device failed, device name: {0}   file: {1}", item.Key, deviceFile);
                    //}
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("load device table {0} exception, {1}", deviceFile, ex.Message);
                    throw;
                }
            }

            _logger.LogInformation("end loading device table");
            await Task.Delay(10);
            return variables;
        }

        async Task<List<VariableEntity>> GetDeviceTableFromDBAsync()
        {
            using (HttpClient httpClient = new())
            {
                List<VariableEntity> variables = new List<VariableEntity>();
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    httpClient.BaseAddress = new Uri(IZUConfig.BackendIZUBaseUrl);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.GetAsync($"izu/get/device/vars?id={IZUConfig.ID}");
                    response.EnsureSuccessStatusCode();

                    string result = await response.Content.ReadAsStringAsync();
                    var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                    if (!resultObject!.ok)
                        throw new Exception(resultObject.message);

                    _logger.LogInformation($"download izu device table successfully");
                    var arr = Newtonsoft.Json.JsonConvert.DeserializeObject<JArray>(resultObject!.data!.ToString()!);
                    foreach (var item in arr!)
                    {
                        variables.Add(new VariableEntity
                        {
                            ServerIP = $"{item["ip"]}",
                            DeviceName = $"{item["name"]}",
                            DeviceType = (DeviceTypes)(int)item["device_type"]!,
                            FunctionType = (FunctionTypes)(int)item["function"]!,
                            ActionType = $"{item["bindary"]}",
                            Address = $"{item["address"]}",
                            VariableType = (VariableTypes)(int)item["data_type"]!,
                            Description = $"{item["description"]}",
                            Disabled = $"{item["status"]}" == "disabled",
                            RefreshInterval = $"{item["refresh"]}".ToInt32(100)
                        });
                    }
                }
                catch (HttpRequestException http_ex)
                {
                    _logger.LogWarning($"download izu device table failed: {http_ex.Message}");
                }
                return variables;
            }
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

        public void RefreshConfig()
        {
            foreach (var deviceEntity in GetAllDevices())
            {
                deviceEntity.Refresh(100);
            }
        }

    }
}
