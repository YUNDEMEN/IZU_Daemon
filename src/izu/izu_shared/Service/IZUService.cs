using IZU.Base;
using IZU.Interfaces;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json.Nodes;
using TinyCsvParser;
using TinyCsvParser.Reflection;
using Wonder.Infrastructure;
using Wonder.Service.Framework;

namespace IZU.Service
{
    [Regist(RegisterTypes.Singleton)]
    public class IZUService : IIZUService
    {
        private readonly ILogger<IZUService> logger;
        private readonly IWebSocketService webSocketService;
        private readonly IS7NetService s7netService;
        private readonly List<string> logs;
        public List<string> Logs { get { return logs; } }
        public IZUService(ILogger<IZUService> logger, IS7NetService s7netService, IWebSocketService webSocketService)
        {
            this.logger = logger;
            this.s7netService = s7netService;
            this.webSocketService = webSocketService;
            logs = new List<string>();
        }

        void Log(string message, LogLevel logLevel = LogLevel.Information)
        {
            if (string.IsNullOrEmpty(message)) { return; }
            logger.Log(logLevel, message);
            logs.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}]:  {message}");
        }

        /*
                 启动流程：
                 1. 读取本地配置
                 2. 获取地图版本（izu/map）
                 3. 读取远程配置（izu/exist?n= 、izu/existById、izu/add、izu/edit）
                    调用 izu/exist?n= 判断当前 izu 服务器是否存在（根据当前 izu ip address 检索）
                        如果存在， 获取wspub_interval、izu_id（更新本地izu id）
                        如果不存在
                            如果本地 izu id 不存在 ，调用 izu/add 添加当前 izu 服务器（获取izu_id，更新本地izu id）
                            如果本地 izu id 存在 ，调用 izu/existById 判断 izu id 是否存在
                                如果不存在，调用 izu/add 添加当前 izu 服务器（获取izu_id）
                                如果存在，调用 izu/edit 更新本地 izu ip 
                 4. 读取变量表（本地/远程）
                 5. 启动 S7NetService
        */
        public async Task StartAsync()
        {
            logs.Clear();
            Log("service is starting");
            Log(IZUConfig.Read());
            await GetMapVersion();
            await ReadConfigFromDBAsync();
            Log(IZUConfig.Read());
            Log("current config is:\r\n" + IZUConfig.ToString());

            var device_var_list = await GetDeviceVariables();
            s7netService.Start(device_var_list);
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
                    Log($"begin get map version...(call api: izu/map)");
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
                                Log($"write map version to json failed.", LogLevel.Warning);
                            }
                            else
                                Log($"current map version is \"{IZUConfig.MapVersion}\"");
                        }
                    }
                }
                catch (HttpRequestException http_ex)
                {
                    Log($"get map version failed: {http_ex.Message}", LogLevel.Warning);
                }
                catch (Exception ex)
                {
                    Log($"get map version failed: {ex.Message}", LogLevel.Warning);
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
            Log($"begin get izu info... (call api: izu/exist?n={IZUConfig.Server})");
            Log($"current local izu id={IZUConfig.izuId}");
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
                            Log($"remote izu id={izu_id}");
                        }
                    }

                    if (izu_id == 0)
                    {
                        Log($"get remote izu id failed");
                        if (string.IsNullOrEmpty(IZUConfig.Read()))
                        {
                            if (IZUConfig.izuId == 0)
                            {
                                Log($"local izu id=0, try to add local ip={IZUConfig.Server} to server (call api: izu/add)");
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
                                        Log($"add izu ip={IZUConfig.Server} failed: {resultObject.message}", LogLevel.Warning);
                                    }
                                    else
                                    {
                                        int.TryParse($"{resultObject.data}", out izu_id);
                                        IZUConfig.izuId = izu_id;
                                        Log($"add izu ip={IZUConfig.Server} successfully, current izu id={izu_id}");
                                        if (!IZUConfig.WriteToAppSetting("izuId", izu_id))
                                            Log($"update local izu id failed.  local temp izu id={IZUConfig.izuId}", LogLevel.Warning);
                                    }
                                }
                            }
                            else
                            {
                                Log($"local izu id={IZUConfig.izuId}, try to check remote id (call api: izu/existById)");
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
                                        Log($"check remote izu id failed: {resultObject.message}", LogLevel.Warning);
                                    }
                                    else
                                    {
                                        if ((bool)resultObject.data == false)
                                        {
                                            Log($"local izu id={IZUConfig.izuId} does not exist in remote server. try to add local ip={IZUConfig.Server} to server (call api: izu/add)");
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
                                                    Log($"add izu ip={IZUConfig.Server} failed: {resultObject.message}", LogLevel.Warning);
                                                }
                                                else
                                                {
                                                    int.TryParse($"{resultObject.data}", out izu_id);
                                                    IZUConfig.izuId = izu_id;
                                                    Log($"add izu ip={IZUConfig.Server} successfully, current izu id={izu_id}");
                                                    if (!IZUConfig.WriteToAppSetting("izuId", izu_id))
                                                        Log($"update local izu id failed.  local temp izu id={IZUConfig.izuId}", LogLevel.Warning);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Log($"local izu id={IZUConfig.izuId} exists in remote server");
                                        }
                                    }
                                }

                                Log($"try to update izu id={IZUConfig.izuId}, ip={IZUConfig.Server} to server (call api: izu/edit)");
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
                                        Log($"update izu id={IZUConfig.izuId}, ip={IZUConfig.Server} failed: {resultObject.message}", LogLevel.Warning);
                                    }
                                    else
                                    {
                                        Log($"update izu id={IZUConfig.izuId}, ip={IZUConfig.Server} to remote server successfully");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        IZUConfig.izuId = izu_id;
                        if (!IZUConfig.WriteToAppSetting("izuId", izu_id))
                            Log($"update local izu id={izu_id} failed.  local temp izu id={IZUConfig.izuId}", LogLevel.Warning);
                        else
                            Log($"update local izu id successfully");
                    }

                    webSocketService.Refresh();
                }
                catch (HttpRequestException http_ex)
                {
                    Log($"get izu info failed: {http_ex.Message}", LogLevel.Warning);
                }
                catch (Exception ex)
                {
                    Log($"get izu info failed: {ex.Message}", LogLevel.Warning);
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
            Log($"begin getting devices table from {IZUConfig.DeviceTableFrom}", LogLevel.Warning);
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
            if (IZUConfig.izuId == 0)
            {
                Log($"current izu id={IZUConfig.izuId}, can not get devices from remote server", LogLevel.Warning);
                return new List<VariableEntity>();
            }
            using (HttpClient httpClient = new())
            {
                List<VariableEntity> variables = new List<VariableEntity>();
                try
                {
                    Log($"try to get devices from remote server. (call api: izu/get/device/vars/by?id={IZUConfig.izuId})");
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    httpClient.BaseAddress = new Uri(IZUConfig.BackendIZUBaseUrl);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.GetAsync($"izu/get/device/vars/by?id={IZUConfig.izuId}");
                    response.EnsureSuccessStatusCode();

                    string result = await response.Content.ReadAsStringAsync();
                    var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                    if (!resultObject!.ok)
                        throw new Exception(resultObject.message);

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
                    Log($"get devices table successfully, total variables: {arr.Count}");
                }
                catch (HttpRequestException http_ex)
                {
                    Log($"get devices table failed: {http_ex.Message}", LogLevel.Warning);
                }
                catch (Exception ex)
                {
                    Log($"get devices table failed: {ex.Message}", LogLevel.Warning);
                }
                return variables;
            }
        }

        async Task<List<VariableEntity>> GetDeviceTableFromLocalFileAsync()
        {
            Log("start loading local devices table. location: .\\DeviceTable\\*.csv");

            DirectoryInfo dir = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeviceTable"));
            if (!dir.Exists)
            {
                Log("devices table is missing", LogLevel.Warning);
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
                            Log($"load devices table {deviceFile} error, {item.Error}", LogLevel.Warning);
                            continue;
                        }
                        variables.Add(item.Result);
                    }
                    Log($"device table loaded {deviceFile}, total variables: {variables.Count}");
                }
                catch (Exception ex)
                {
                    Log($"load devices table {deviceFile} error: {ex.Message}", LogLevel.Error);
                    //throw;
                }
            }

            Log("end loading");
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
            var devices = s7netService.GetAllDevices();
            if (devices.Count > 0)
                Log($"try to update devices izu id={IZUConfig.izuId} to remote server({devices.Count}). (call api: izu/update/devices)");
            else
            {
                Log($"devices table is empy", LogLevel.Warning);
                return;
            }
            using (HttpClient httpClient = new())
            {
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    httpClient.BaseAddress = new Uri(IZUConfig.BackendIZUBaseUrl);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));                  
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
                                Log($"upload devices izu id={IZUConfig.izuId} failed: {resultObject.message}", LogLevel.Warning);
                            }
                            else
                            {
                                Log($"upload devices izu id={IZUConfig.izuId} successfully");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"check devices failed: {ex.Message}", LogLevel.Warning);
                }
            }
        }

        public void Stop()
        {
            webSocketService.Stop();
            s7netService.Stop();
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