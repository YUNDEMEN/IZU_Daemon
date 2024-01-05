using IZU.Base;
using IZU.DeviceFactories;
using IZU.Entities;
using IZU.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;

namespace IZU.Service
{
    public class IZUService : IIZUService
    {
        public ServiceRuntime ServiceRuntime { get; }
        private readonly ILogger<IZUService> _logger;
        private readonly Timer _timer;
        private readonly IIZUBroadcastServer _broadcastServer;

        public IS7NetService S7netService { get; }
        public IZUService(ILoggerFactory loggerFactory, ILogger<IZUService> logger, IS7NetService s7netService, IIZUBroadcastServer broadcastServer)
        {
            IZULogging.ConfigureLogger(loggerFactory);
            _logger = logger;
            S7netService = s7netService;
            _broadcastServer = broadcastServer;
            _timer = new Timer(Callback);
            ServiceRuntime = new ServiceRuntime();
            logger.LogInformation("IZU service initialized");
        }
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

            await ReadConfigFromDBAsync();

            await S7netService.StartAsync();

            _logger.LogInformation("---------------IZU service started---------------");

        }

        public async Task ReadConfigFromDBAsync()
        {
            _logger.LogInformation($"begin get izu info...");
            int izu_id = 0;

            using (HttpClient httpClient = new())
            {
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    httpClient.BaseAddress = new Uri(IZUConfig.BackendIZUBaseUrl);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.GetAsync($"izu/exist?n={IZUConfig.Server}");
                    if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        var jsonResult = JsonObject.Parse(result);
                        var resultObject = Newtonsoft.Json.JsonConvert.DeserializeObject<response_object>(result);
                        if (resultObject!.ok)
                        {
                            JObject izuObj = resultObject.data as JObject;
                            IZUConfig.PublishMillionSeconds = (int)izuObj["wspub_interval"]!;
                            izu_id = (int)izuObj["id"];
                            _logger.LogInformation($"get izu info successfully");
                        }
                    }

                    if (izu_id == 0)
                    {
                        response = await httpClient.PostAsync($"izu/add", JsonContent.Create(new { name = IZUConfig.Server, ip = IZUConfig.Server }));
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
                            }
                        }
                    }
                    if (izu_id > 0)
                    {
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

        public void RefreshConfig()
        {
            S7netService.RefreshConfig();
            _broadcastServer.Refresh();
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