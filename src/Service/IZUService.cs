using IZU.Base;
using IZU.DeviceFactories;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace IZU.Service
{
    public class IZUService : IIZUService
    {
        public ServiceRuntime ServiceRuntime { get; }
        public IZUConfig _config { get; private set; }
        private readonly ILogger<IZUService> _logger;
        private readonly Timer _timer;
        private readonly IIZUBroadcastServer _broadcastServer;

        public IS7NetService S7netService { get; }
        public IZUConfig Config { get { return _config; } }
        public IZUService(ILoggerFactory loggerFactory, ILogger<IZUService> logger, IOptions<IZUConfig> cfg, IS7NetService s7netService, IIZUBroadcastServer broadcastServer)
        {
            IZULogging.ConfigureLogger(loggerFactory);
            _logger = logger;
            _config = cfg.Value;
            S7netService = s7netService;
            _broadcastServer = broadcastServer;
            _timer = new Timer(Callback);
            ServiceRuntime = new ServiceRuntime();
            logger.LogInformation("IZU service initialized");
        }
        record class response_object(object data, bool ok, string message);
        public async Task StartAsync()
        {
            var _loggers = IZULogging.Factory.CreateLogger<Device>();
            //Task.Run(async () => {
            //    while (true)
            //    {
            //        _logger.LogDebug("---------------LogInformation 这是一个八哥---------------");
            //        _logger.LogInformation("---------------LogInformation---------------");
            //        _logger.LogWarning("---------------LogWarning---------------");
            //        _logger.LogError("---------------LogError---------------");
            //        _logger.LogCritical("---------------LogError---------------");
            //        NLog.LogManager.GetCurrentClassLogger().Info("nlog info");
            //        await Task.Delay(2000);
            //    }
            //});
            _logger.LogInformation("---------------IZU service starting---------------");
            _timer.Change(1000, 1000);
            await S7netService.StartAsync();
            await UploadIZUInfo2DatabaseAsync();
            _logger.LogInformation("---------------IZU service started---------------");

        }

        public async Task UploadIZUInfo2DatabaseAsync()
        {
            _logger.LogInformation($"begin upload izu info...");
            int izu_id = 0;

            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    httpClient.BaseAddress = new Uri(_config.izu_backend);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.GetAsync($"izu/exist?n={_config.Name}");
                    if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        var jsonResult = JsonObject.Parse(result);
                        var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                        if (resultObject!.ok)
                        {
                            int.TryParse($"{resultObject.data}", out izu_id);
                            _logger.LogInformation($"upload izu info successfully");
                        }
                    }

                    if (izu_id == 0)
                    {
                        response = await httpClient.PostAsync($"izu/add", JsonContent.Create(new { name = _config.Name, ip = _config.Server }));
                        if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                        {
                            string result = await response.Content.ReadAsStringAsync();
                            var jsonResult = JsonObject.Parse(result);
                            var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                            if (!resultObject!.ok)
                            {
                                _logger.LogInformation($"add izu info failed: {resultObject.message}");
                            }
                            else
                            {
                                int.TryParse($"{resultObject.data}", out izu_id);
                                _logger.LogInformation($"add izu info successfully");
                            }
                        }
                    }
                    if (izu_id > 0)
                    {
                        response = await httpClient.PostAsync($"izu/edit", JsonContent.Create(new { id = izu_id, name = _config.Name, ip = _config.Server }));
                        if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                        {
                            string result = await response.Content.ReadAsStringAsync();
                            var jsonResult = JsonObject.Parse(result);
                            var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                            if (!resultObject!.ok)
                            {
                                _logger.LogInformation($"update izu info failed: {resultObject.message}");
                            }
                            else
                            {
                                _logger.LogInformation($"update izu info successfully");
                            }
                        }

                        /*
                         由于设备的数量和名称需要以地图标注为主，所以变量表中的设备名称需要与地图保持一致
                         以下方法是以设备名称为条件，更新每一条设备信息的izu id和网络地址ip。
                               其中，izu_id 需要手动设置，位于配置中的izu节点name字段
                                         ip字段为通讯地址（目前是所属izu的地址）
                        */
                        var devices = S7netService.GetAllDevices();
                        var groups = from x in devices where x.DeviceType != DeviceTypes.IZU && x.DeviceType != DeviceTypes.NONE group x by x.DeviceType;
                        foreach (var item in groups)
                        {
                            var ds = from x in item.ToList()
                                     select new
                                     {
                                         name = x.Name,
                                         ip = x.Server!.IP,
                                         id = izu_id
                                     };
                            response = await httpClient.PostAsync($"izu/update/devices", JsonContent.Create(ds));
                            if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                            {
                                string result = await response.Content.ReadAsStringAsync();
                                response_object? resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                                if (!resultObject!.ok)
                                {
                                    _logger.LogInformation($"upload izu {item.Key} info failed: {resultObject.message}");
                                }
                                else
                                {
                                    _logger.LogInformation($"upload izu {item.Key} info successfully");
                                }
                            }
                        }
                    }

                }
                catch (HttpRequestException http_ex)
                {
                    _logger.LogWarning($"upload izu info error: {http_ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"upload izu info error: {ex.Message}");
                }
                finally
                {
                    httpClient.Dispose();
                }
            }
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

        public void RefreshConfig(IZUConfig config)
        {
            _config = config;
            S7netService.RefreshConfig(config);
            _broadcastServer.Refresh(config);
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