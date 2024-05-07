using IZU.Base;
using IZU.Base.dto;
using IZU.Interfaces;
using System.Collections.Concurrent;
using Wonder.Service.Framework;

namespace IZU.Service
{
    [Regist(RegisterTypes.Singleton)]
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
        public void Start(List<VariableEntity> variables)
        {
            _cDic.Clear();
            var groups = variables.GroupBy(t => t.DeviceName, t => t);
            foreach (var item in groups)
            {
                if (string.IsNullOrEmpty(item.Key)) continue;
                if (!_cDic.TryAdd(item.Key.ToUpper(), new DeviceEntity(_loggerFactory, IZUConfig.DeviceTableFrom, item.Key, item.ToList())))
                    _logger.LogWarning("add device failed, device name: {0}   file: {1}", item.Key, IZUConfig.DeviceTableFrom);
            }
        }

#if false
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
                    HttpResponseMessage response = await httpClient.GetAsync($"izu/get/device/vars/by?id={IZUConfig.ID}");
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
                catch(Exception ex)
                {
                    _logger.LogWarning($"download izu device table failed: {ex.Message}");
                }
                return variables;
            }
        }
#endif
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

        public izu_status GetStatus()
        {
            izu_status status = new();

            // 先读izu自身状态，如果离线，则不再读取error状态
            var izus = _cDic.Values.Where(x => x.DeviceType == DeviceTypes.IZU).ToList();
            // 确保加载完变量表
            if (izus.Count == 0)
            {
                status.offline = true;
                return status;
            }
            // offline判断
            var R01 = izus[0].Variables.FirstOrDefault(p => p.ActionType == "R01")?.Value;
            if (R01 == null || ((bool)R01).ToString() == "False")
            {
                status.offline = true;
                return status;
            }

            // error判断
            foreach (var device in _cDic.Values)
            {
                switch (device.DeviceType)
                {
                    case DeviceTypes.IZU:
                        var list = device.Variables;
                        foreach (var item in list)
                        {
                            // IZU系统报错、硬件通讯端口连接异常、IZU失电状态
                            var R05 = list.FirstOrDefault(p => p.ActionType == "R05")?.Value;
                            var R06 = list.FirstOrDefault(p => p.ActionType == "R06")?.Value;
                            var R08 = list.FirstOrDefault(p => p.ActionType == "R08")?.Value;
                            // 8个HID通讯异常
                            var R11 = list.FirstOrDefault(p => p.ActionType == "R11")?.Value;
                            var R12 = list.FirstOrDefault(p => p.ActionType == "R12")?.Value;
                            var R13 = list.FirstOrDefault(p => p.ActionType == "R13")?.Value;
                            var R14 = list.FirstOrDefault(p => p.ActionType == "R14")?.Value;
                            var R15 = list.FirstOrDefault(p => p.ActionType == "R15")?.Value;
                            var R16 = list.FirstOrDefault(p => p.ActionType == "R16")?.Value;
                            var R17 = list.FirstOrDefault(p => p.ActionType == "R17")?.Value;
                            var R18 = list.FirstOrDefault(p => p.ActionType == "R18")?.Value;
                            // 2个autodoor通讯异常
                            var R19 = list.FirstOrDefault(p => p.ActionType == "R19")?.Value;
                            var R20 = list.FirstOrDefault(p => p.ActionType == "R20")?.Value;
                            if (R05 == null || ((bool)R05).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R06 == null || ((bool)R06).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R08 == null || ((bool)R08).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R11 == null || ((bool)R11).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R12 == null || ((bool)R12).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R13 == null || ((bool)R13).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R14 == null || ((bool)R14).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R15 == null || ((bool)R15).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R16 == null || ((bool)R16).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R17 == null || ((bool)R17).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R18 == null || ((bool)R18).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R19 == null || ((bool)R19).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R20 == null || ((bool)R20).ToString() == "True") status.error.izu.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                        }
                        break;
                    case DeviceTypes.AUTODOOR:
                        var list2 = device.Variables;
                        foreach (var item in list2)
                        {
                            // Auto Door 故障报错状态
                            var R09 = list2.FirstOrDefault(p => p.ActionType == "R09")?.Value;
                            if (R09 == null || ((bool)R09).ToString() == "True") status.error.autodoor.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                        }
                        break;
                    case DeviceTypes.HID:
                        var list3 = device.Variables;
                        foreach (var item in list3)
                        {
                            // PSP温度异常过高、电柜失电状态信号、PSP故障报警状态、火警信号
                            var R04 = list3.FirstOrDefault(p => p.ActionType == "R04")?.Value;
                            var R06 = list3.FirstOrDefault(p => p.ActionType == "R06")?.Value;
                            var R08 = list3.FirstOrDefault(p => p.ActionType == "R08")?.Value;
                            var R10 = list3.FirstOrDefault(p => p.ActionType == "R10")?.Value;
                            if (R04 == null || ((bool)R04).ToString() == "True") status.error.hid.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R06 == null || ((bool)R06).ToString() == "True") status.error.hid.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R08 == null || ((bool)R08).ToString() == "True") status.error.hid.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                            if (R10 == null || ((bool)R10).ToString() == "True") status.error.hid.info.Add(new info() { ip = item.ServerIP, address = item.Address, description = item.Description });
                        }
                        break;
                }
            }
            return status;
        }

        public DeviceEntity? GetDevice(string deviceName)
        {
            _ = _cDic.TryGetValue(deviceName.ToUpper(), out var device);
            return device;
        }

        public List<VariableEntity> GetDeviceVariables(string deviceName)
        {
            DeviceEntity? device = GetDevice(deviceName.ToUpper());
            if (device == null) return new List<VariableEntity>();
            return device.Variables;
        }
    }
}
