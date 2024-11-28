using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NNanomsg.Protocols;
using Quartz;
using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using IZU.Base;

namespace izu.moxa
{
    /*
     * http://localhost/apiGuide
     * http://192.168.58.129/apiGuide
     *GET  /api/devices                                                       GET all devices
     * 
     *GET  /api/siteEvents/site/{siteId}                                Get {count} recent site events
     *GET  /api/sites                                                            Get all unhidden sites
     *GET  /api/sites/online                                                 Get all online sites
     * 
     * 
     *GET  /api/events/site/{siteId}                                      Get {count} recent events   query :  count  severity  acked
     *GET  /api/events/site/{siteId}/type/{type}                   Get {type} events from a site.    
     *GET  /api/events/type/{type}                                      Get {type} events from all sites.
     *GET  /api/events/site/{siteId}/current                         Get all events which is currently triggered from a site.
     *GET  /api/events/site/{siteId}/type/{type}/current      Get {type} events which is currently triggered from a site.
     *
     */
    public class MoxaJob : IJob
    {
        readonly ILogger<MoxaJob> _logger;
        private RequestSocket reqSock;
        PostData postData = new PostData
        {
            ids = new List<int>()
        };
        record moxaResp(string errno, string message, bool ok, JArray data);
        public MoxaJob(ILogger<MoxaJob> logger)
        {
            _logger = logger;
            reqSock = new RequestSocket();
            reqSock.Options.SendTimeout = TimeSpan.FromSeconds(2);
            reqSock.Options.ReceiveTimeout = TimeSpan.FromSeconds(1);
            reqSock.Connect(IZUConfig.OSOChannel);
            _logger.LogInformation("Moxa Watcher Initialized");
        }

        
        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("--------------------------Moxa Job Begin Executing--------------------------");
            string siteid = string.Empty;

            using HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            httpClient.BaseAddress = new Uri(IZUConfig.MXviewHost);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            /*不需要,使用固定token
            #region 登录获取token

            string error = string.Empty;
            if (string.IsNullOrEmpty(IZUConfig.MXviewToken))
            {
                (IZUConfig.MXviewToken, error) = await MoxaLoginAsync(httpClient);
            }
            if (string.IsNullOrEmpty(IZUConfig.MXviewToken))
            {
                return;
            }

            #endregion
            */
            
            #region 获取Site ID
            moxaResp sitesResp = await GetJArrayAsync(httpClient, "/api/sites", IZUConfig.MXviewToken);
            if (!sitesResp.ok) { return; }
            else
            {
                foreach (var site in sitesResp.data)
                {
                    siteid = $"{site["site_id"]}";
                    if (string.IsNullOrWhiteSpace(siteid))
                    {
                        continue;
                    }
                    else
                    {
                        _logger.LogInformation("current site id  : {0}", siteid);
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(siteid))
            {
                return;
            }
            #endregion
            

            #region 获取 Site 事件
            /*
             http://127.0.0.1:8089/api/events/site/lqDXhHcKjXTyftBa?severity=3&acked=false
             设备断电类型：
                   65542 0x00010006  Warning 電源 x 斷電 Power x is down x: The nth power input(1 or 2)	{ trap_detail: x}
                   65543 0x00010007  Warning 設備無法以SNMP存取 Device SNMP unreachable
             设备掉线类型
                   65537	0x00010001	Critical	設備無法存取	Device ICMP unreachable		

             设备上电类型：
                   2147549190    0x80010006  Normal(Information)    電源 x 通電 Power x is up x: The nth power input(1 or 2)	{ trap_detail: x}
                   2147549191    0x80010007  Normal(Information)    設備恢復以SNMP存取 Device SNMP reachable
                   2147549185	0x80010001	Normal (Information)	設備恢復存取	Device ICMP reachable		

             用户登录失败类型
                   268435478  测试用

             逻辑：分别读取断电和上电

            {
                "type": 268435477,
                "event_time": 1703495418,
                "security_event": false,
                "id": 83,
                "device_id": 0,
                "source_ip": "127.0.0.1",
                "severity": 4,
                "vpn_connectionname": "",
                "port": 0,
                "detector": 1,
                "threshold_type": 0,
                "user": "admin",
                "value": 0,
                "acked": false,
                "site_id": "84f16950dfa6386b"
            }
          */
            
            moxaResp respEvents = await GetJArrayAsync(httpClient, $"/api/events/site/{siteid}?acked=false&count=100", IZUConfig.MXviewToken);
            if (!respEvents.ok) { return; }
            else
            {
                string[] conditions = new string[] { "65537","2147549185"};
                var sortedEvents = from x in respEvents.data
                                   where x["type"] != null && conditions.Contains(x["type"]!.ToString().Trim())
                                   orderby long.Parse(x["event_time"]!.ToString()) ascending
                                   select x;
                List<string> ids = new List<string>();
                if (!sortedEvents.Any())
                {
                    //device is alright?
                    _logger.LogInformation("no device event observed");
                    return;
                }
                else
                {
                    foreach(var sortedEvent in sortedEvents)
                    {
                        postData.ids.Add(int.Parse(sortedEvent["id"]!.ToString()));
                        string ip = sortedEvent["source_ip"]!.ToString();
                        MXDev? dev = IZUConfig.MXDevList.Where(d => d.MXDevIP == ip).FirstOrDefault();
                        if (dev == null)
                            continue;
                        string device_id = sortedEvent["device_id"]!.ToString();
                        switch (sortedEvent["type"]!.ToString())
                        {
                            case "65537":
                                //断电
                                if (!respEvents.ok) { return; }
                                var updateMoxaOffline = new
                                {
                                    opname = "IZU_MOXA_STATE",
                                    opparas = new
                                    {
                                        name = dev.Name,
                                        IP = dev.MXDevIP,
                                        status = false,
                                        isoht = dev.IsOHT
                                    }
                                };
                                reqSock.Send(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(updateMoxaOffline)));
                                _logger.LogCritical("{0} is offline", ip);
                                break;
                            case "2147549185":
                                //上电
                                if (!respEvents.ok) { return; }
                                var updateMoxaOnline = new
                                {
                                    opname = "IZU_MOXA_STATE",
                                    opparas = new
                                    {
                                        name = dev.Name,
                                        IP = dev.MXDevIP,
                                        status = true,
                                        isoht = dev.IsOHT
                                    }
                                };
                                reqSock.Send(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(updateMoxaOnline)));
                                _logger.LogCritical("{0} is online", ip);
                                break;
                        }
                    }
                    //修改事件状态为已确认
                    if (postData.ids.Any())
                    {
                        moxaResp postRespEvent = await PostJArrayAsync(httpClient, $"/api/events/site/{siteid}/ack", IZUConfig.MXviewToken, postData);
                        if (postRespEvent.ok)
                        {
                            postData.ids.Clear();
                        }
                    }
                }
            }

            #endregion
                    

            //string api = $"{context.JobDetail.JobDataMap.GetString("api")}";
            //string[] parameters = $"{context.JobDetail.JobDataMap.GetString("queries")}".Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            //await GetSiteEventsAsync(httpClient,tokenValue, host, api, parameters);
            
            _logger.LogInformation("--------------------------Moxa Job Finfished--------------------------");
        }
        

        //http://127.0.0.1/api/events/site/84f16950dfa6386b?count=50
        //84f16950dfa6386b
        async Task<(string, string)> MoxaLoginAsync(HttpClient httpClient)
        {
            try
            {
                HttpResponseMessage response = await httpClient.PostAsync($"login", JsonContent.Create(new
                {
                    username = "admin",
                    password = "moxa"
                }));
                if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();
                    var resultObject = JObject.Parse(result);
                    string token = resultObject["mxviewGateway"]!.ToString();
                    if (!string.IsNullOrEmpty(token))
                        _logger.LogInformation("admin login successfully");
                    return (token, string.Empty);
                }
            }
            catch (Exception ex)
            {
                string err = $"login FAILED: {ex.Message}";
                _logger.LogError(err);
                return (string.Empty, err);
            }
            return (string.Empty, "some reason");
        }

        async Task<moxaResp> GetJArrayAsync(HttpClient httpClient, string api, string tokenValue)
        {
            try
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
                HttpResponseMessage response = await httpClient.GetAsync(api);
                string result = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    moxaResp resp = new("", "", true, JArray.Parse(result));
                    return resp;
                    //Console.WriteLine(resultObject);
                }
                else
                {
                    moxaResp? resp = JsonConvert.DeserializeObject<moxaResp>(result);
                    _logger.LogError("{0} FAILED, {2}({1})", api, resp!.errno, resp.message);
                    return new moxaResp(resp.errno, resp.message, false, new JArray());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("{0} FAILED, {1}", api, ex.StackTrace!.ToString());
            }
            return new moxaResp("", "", false, new JArray());
        }

        async Task<moxaResp> PostJArrayAsync(HttpClient httpClient, string api, string tokenValue, object payload)
        {
            try
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
                string jsonPayload = JsonConvert.SerializeObject(payload);
                HttpContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await httpClient.PostAsync(api, content);
                string result = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    moxaResp resp = new("", "", true, JArray.Parse(result));
                    return resp;
                }
                else
                {
                    moxaResp? resp = JsonConvert.DeserializeObject<moxaResp>(result);
                    _logger.LogError("{0} FAILED, {2}({1})", api, resp!.errno, resp.message);
                    return new moxaResp(resp.errno, resp.message, false, new JArray());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("{0} FAILED, {1}", api, ex.StackTrace!.ToString());
            }
            return new moxaResp("", "", false, new JArray());
        }
        async Task GetSiteEventsAsync(HttpClient httpClient, string tokenValue, string host, string api, params string[] parameters)
        {
            try
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
                string apiQuery = string.Format(api, parameters);//$"api/events/site/84f16950dfa6386b?count=50"
                HttpResponseMessage response = await httpClient.GetAsync(apiQuery);
                if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();
                    var resultObject = JArray.Parse(result);
                    //Console.WriteLine(resultObject);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("{0} FAILED, {1}", api, ex.StackTrace.ToString());
            }
        }
    }

    public class PostData
    {
        public List<int> ids { get; set; }
    }
}
