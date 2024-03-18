using IZU.Base;
using IZU.Interfaces;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json.Nodes;
using TinyCsvParser;
using Wonder.Infrastructure;
using Wonder.Service.Framework;

namespace IZU.Service
{
    [Regist(RegisterTypes.Singleton)]
    public class IZUService : IIZUService
    {
        private readonly IServiceRuntime _serviceRuntime;
        private readonly ILogger<IZUService> _logger;
        private readonly IWebSocketService _webSocketService;
        private readonly IS7NetService _s7netService;

        public IZUService(ILogger<IZUService> logger, IServiceRuntime serviceRuntime, IS7NetService s7netService, IWebSocketService webSocketService)
        {
            _logger = logger;
            _s7netService = s7netService;
            _webSocketService = webSocketService;
            _serviceRuntime = serviceRuntime;
            logger.LogInformation("IZU service initialized");
        }

        public async Task StartAsync()
        {
            _serviceRuntime.MarkStarted();
            IZUConfig.Read();
            await GetMapVersion();
            await ReadConfigFromDBAsync();
            IZUConfig.Read();

            var device_var_list = await GetDeviceVariables();
            _s7netService.Start(device_var_list);
            await CheckDevices();
        }
        /// <summary>
        /// 获取地图版本
        /// </summary>
        /// <returns></returns>
        private async Task GetMapVersion()
        {
            using (HttpClient httpClient = new())
            {
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    httpClient.BaseAddress = new Uri(IZUConfig.BackendIZUBaseUrl);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.GetAsync($"izu/map");
                    if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        var jsonResult = JsonObject.Parse(result);
                        var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                        if (resultObject != null && resultObject.ok && resultObject.data != null)
                        {
                            IZUConfig.MapVersion = $"{resultObject.data}";
                            if (!IZUConfig.WriteToAppSetting("map_version", IZUConfig.MapVersion))
                            {
                                _logger.LogWarning($"write mapVersion to json failed.");
                            }
                            else
                                _logger.LogInformation($"current mapVersion is {IZUConfig.MapVersion}");
                        }
                    }
                }
                catch (HttpRequestException http_ex)
                {
                    _logger.LogWarning($"get mapVersion error: {http_ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"get mapVersion error: {ex.Message}");
                }
                finally
                {
                    httpClient.Dispose();
                }
            }
        }

        /// <summary>
        /// 获取远程配置
        /// 1. izu id
        /// 2. wspub_interval
        /// </summary>
        /// <returns></returns>
        public async Task ReadConfigFromDBAsync()
        {
            _logger.LogInformation($"begin get izu info...");
            int izu_id = 0;
            using (HttpClient httpClient = new())
            {
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    httpClient.BaseAddress = new Uri(IZUConfig.BackendIZUBaseUrl);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.GetAsync($"izu/exist?n={IZUConfig.Server}");
                    if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        var jsonResult = JsonObject.Parse(result);
                        var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                        if (resultObject!.ok && resultObject.data != null)
                        {
                            JObject izuObj = (JObject)resultObject.data;
                            IZUConfig.PublishMillionSeconds = (int)izuObj["wspub_interval"]!;
                            izu_id = (int)izuObj["id"]!;
                            _logger.LogInformation($"get izu info successfully");
                            //if ($"{izuObj["status"]}" == "disabled")
                            //    return;
                        }
                    }

                    if (izu_id == 0)
                    {
                        if (string.IsNullOrEmpty(IZUConfig.Read()))
                        {
                            if (IZUConfig.izuId == 0)
                            {
                                response = await httpClient.PostAsync($"izu/add", JsonContent.Create(new
                                {
                                    ip = IZUConfig.Server,
                                    ws_interval = 100
                                }));
                                if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                                {
                                    string result = await response.Content.ReadAsStringAsync();
                                    var jsonResult = JsonObject.Parse(result);
                                    var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                                    if (!resultObject!.ok)
                                    {
                                        _logger.LogWarning($"add izu info failed: {resultObject.message}");
                                    }
                                    else
                                    {
                                        int.TryParse($"{resultObject.data}", out izu_id);
                                        _logger.LogInformation($"add izu info successfully");
                                        IZUConfig.izuId = izu_id;
                                        if (!IZUConfig.WriteToAppSetting("izuId", izu_id))
                                            _logger.LogWarning($"add izuId to json failed.");
                                    }
                                }
                            }
                            else
                            {
                                // 判断是否存在该id
                                response = await httpClient.PostAsync($"izu/existById", JsonContent.Create(new
                                {
                                    id = IZUConfig.izuId
                                }));
                                if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                                {
                                    string result = await response.Content.ReadAsStringAsync();
                                    response_object? resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                                    if (!resultObject!.ok)
                                    {
                                        _logger.LogWarning($"get bool existById {izu_id} failed: {resultObject.message}");
                                    }
                                    else
                                    {
                                        _logger.LogInformation($"get bool existById {izu_id} successfully");
                                        if ((bool)resultObject.data == false)
                                        {
                                            response = await httpClient.PostAsync($"izu/add", JsonContent.Create(new
                                            {
                                                ip = IZUConfig.Server,
                                                ws_interval = 100
                                            }));
                                            if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                                            {
                                                result = await response.Content.ReadAsStringAsync();
                                                var jsonResult = JsonObject.Parse(result);
                                                resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                                                if (!resultObject!.ok)
                                                {
                                                    _logger.LogWarning($"add izu info failed: {resultObject.message}");
                                                }
                                                else
                                                {
                                                    int.TryParse($"{resultObject.data}", out izu_id);
                                                    _logger.LogDebug($"add izu info successfully");
                                                    IZUConfig.izuId = izu_id;
                                                    if (!IZUConfig.WriteToAppSetting("izuId", izu_id))
                                                        _logger.LogWarning($"add izuId to json failed.");
                                                    else
                                                        _logger.LogInformation($"add izuId to json successfully");
                                                }
                                            }
                                        }
                                    }
                                }

                                response = await httpClient.PostAsync($"izu/edit", JsonContent.Create(new
                                {
                                    id = IZUConfig.izuId,
                                    ip = IZUConfig.Server,
                                    ws_interval = 100
                                }));
                                if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                                {
                                    string result = await response.Content.ReadAsStringAsync();
                                    response_object? resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                                    if (!resultObject!.ok)
                                    {
                                        _logger.LogWarning($"edit izu {izu_id} ipPort config info failed: {resultObject.message}");
                                    }
                                    else
                                    {
                                        _logger.LogInformation($"edit izu {izu_id} ipPort config info successfully");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        IZUConfig.izuId = izu_id;
                        if (!IZUConfig.WriteToAppSetting("izuId", izu_id))
                            _logger.LogWarning($"add izuId to json failed.");
                        else
                            _logger.LogInformation($"add izuId to json successfully");
                    }

                    _webSocketService.Refresh();
                }
                catch (HttpRequestException http_ex)
                {
                    _logger.LogWarning($"get izu info error: {http_ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"get izu info error: {ex.Message}");
                }
                finally
                {
                    httpClient.Dispose();
                }
            }
        }

        /// <summary>
        /// 获取变量表
        /// </summary>
        /// <returns></returns>
        public async Task<List<VariableEntity>> GetDeviceVariables()
        {
            if (IZUConfig.DeviceTableFrom == "db")
            {
                return await GetDeviceTableFromDBAsync();
            }
            else
            {
                return await GetDeviceTableFromLocalFileAsync();
            }
        }

        async Task<List<VariableEntity>> GetDeviceTableFromDBAsync()
        {
            if (IZUConfig.izuId == 0) return new List<VariableEntity>();
            using (HttpClient httpClient = new())
            {
                List<VariableEntity> variables = new List<VariableEntity>();
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    httpClient.BaseAddress = new Uri(IZUConfig.BackendIZUBaseUrl);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.GetAsync($"izu/get/device/vars/by?id={IZUConfig.izuId}");
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
                catch (Exception ex)
                {
                    _logger.LogWarning($"download izu device table failed: {ex.Message}");
                }
                return variables;
            }
        }

        async Task<List<VariableEntity>> GetDeviceTableFromLocalFileAsync()
        {
            _logger.LogInformation("start loading device table");

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
                    }
                    _logger.LogInformation("device table loaded {0}, variable count {1}", deviceFile, variables.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("load device table {0} exception, {1}", deviceFile, ex.Message);
                    //throw;
                }
            }

            _logger.LogInformation("end loading device table");
            await Task.Delay(10);
            return variables;
        }

        /// <summary>
        /// 检查变量表
        /// 更新当前izu id和地图版本
        /// </summary>
        /// <returns></returns>
        async Task CheckDevices()
        {
            using (HttpClient httpClient = new())
            {
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    httpClient.BaseAddress = new Uri(IZUConfig.BackendIZUBaseUrl);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    var devices = _s7netService.GetAllDevices();
                    var groups = from x in devices where x.DeviceType != DeviceTypes.IZU && x.DeviceType != DeviceTypes.NONE group x by x.DeviceType;
                    foreach (var item in groups)
                    {
                        var ds = from x in item.ToList()
                                 select new
                                 {
                                     name = x.Name,
                                     izu_id = IZUConfig.izuId,
                                     map_version = IZUConfig.MapVersion
                                 };
                        var response = await httpClient.PostAsync($"izu/update/devices", JsonContent.Create(ds));
                        if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                        {
                            string result = await response.Content.ReadAsStringAsync();
                            response_object? resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                            if (!resultObject!.ok)
                            {
                                _logger.LogWarning($"upload izu {item.Key} info failed: {resultObject.message}");
                            }
                            else
                            {
                                _logger.LogInformation($"upload izu {item.Key} info successfully");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"check devices error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _webSocketService.Stop();
            _s7netService.Stop();
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