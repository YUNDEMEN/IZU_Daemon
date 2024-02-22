using IZU.Base;
using IZU.Entities;
using IZU.Interfaces;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json.Nodes;
using TinyCsvParser;

namespace IZU.Service
{
    public class IZUService : IIZUService
    {
        public ServiceRuntime ServiceRuntime { get; }
        private readonly ILogger<IZUService> _logger;
        private readonly ICommunication _communicationServer;

        public IS7NetService S7netService { get; }
        public IZUService(ILoggerFactory loggerFactory, ILogger<IZUService> logger, IS7NetService s7netService, ICommunication communicationServer)
        {
            IZULogging.ConfigureLogger(loggerFactory);
            _logger = logger;
            S7netService = s7netService;
            _communicationServer = communicationServer;
            ServiceRuntime = new ServiceRuntime();
            logger.LogInformation("IZU service initialized");
        }
        public async Task StartAsync()
        {
            //var _loggers = IZULogging.Factory.CreateLogger<Device>();
            _logger.LogInformation("---------------IZU service starting---------------");
            IZUConfig.Read();
            await GetMapVersion();
            await ReadConfigFromDBAsync();

            var device_var_list = await GetDeviceVariables();
            S7netService.Start(device_var_list);
            _logger.LogInformation("---------------IZU service started---------------");
            _communicationServer.Start();
        }

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
                        if (resultObject!.ok && resultObject.data != null)
                        {
                            IZUConfig.MapVersion= resultObject.data.ToString();
                            if (!IZUConfig.WriteToAppSetting("map_version", IZUConfig.MapVersion))
                                _logger.LogWarning($"write mapVersion to json failed.");
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
                            if ($"{izuObj["status"]}" == "disabled")
                                return;
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
                                    name = IZUConfig.Server,
                                    ip = IZUConfig.Server,
                                    ws_interval = 100,
                                    backend_url = IZUConfig.BackendIZUBaseUrl
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
                                        _logger.LogDebug($"add izu info successfully");
                                        IZUConfig.izuId = izu_id;
                                        if (!IZUConfig.WriteToAppSetting("izuId",izu_id))
                                            _logger.LogWarning($"add izuId to json failed.");
                                    }
                                }
                            }
                            else 
                            {
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
                                        _logger.LogDebug($"edit izu {izu_id} ipPort config info successfully");
                                    }
                                }
                            }
                        }
                    }

                    if (izu_id > 0)
                    {
                        var devices = S7netService.GetAllDevices();
                        var groups = from x in devices where x.DeviceType != DeviceTypes.IZU && x.DeviceType != DeviceTypes.NONE group x by x.DeviceType;
                        foreach (var item in groups)
                        {
                            var ds = from x in item.ToList()
                                     select new
                                     {
                                         name = x.Name,
                                         izu_id,
                                         map_version = IZUConfig.MapVersion
                                     };
                            response = await httpClient.PostAsync($"izu/update/devices", JsonContent.Create(ds));
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
                                    _logger.LogDebug($"upload izu {item.Key} info successfully");
                                }
                            }
                        }
                    }

                    IZUConfig.ID = izu_id;
                    _communicationServer.Refresh();
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
            if (IZUConfig.ID == 0) return new List<VariableEntity>();
            using (HttpClient httpClient = new())
            {
                List<VariableEntity> variables = new List<VariableEntity>();
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
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
                    _logger.LogDebug("device table loaded {0}, variable count {1}", deviceFile, variables.Count);
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



        void Callback(object? state)
        {
            //Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
            //Console.Write("★★★★★ IZU Service is running! [{0:yyyy-MM-dd HH:mm:ss:fff}] ★★★★★★★", DateTime.Now);
        }


        public void Stop()
        {
            _communicationServer.Stop();
            S7netService.Stop();
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