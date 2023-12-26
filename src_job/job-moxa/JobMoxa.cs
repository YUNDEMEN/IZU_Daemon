using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;
using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace job.moxa
{
    /*
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
    [PersistJobDataAfterExecution]
    public class JobMoxa : IJob
    {
        readonly ILogger<JobMoxa> _logger;
        record moxaResp(string errno,string message,bool ok,JArray data);
        public JobMoxa(ILogger<JobMoxa> logger)
        {
            _logger = logger;
            _logger.LogInformation("Moxa Watcher Initialized");
        }
        public async ValueTask Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("--------------------------Moxa Job Begin Executing--------------------------");

            string tokenValue = $"{context.JobDetail.JobDataMap.GetString("token")}";
            string host = $"{context.JobDetail.JobDataMap.GetString("host")}"; 
            string siteid = string.Empty;

            using HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            httpClient.BaseAddress = new Uri(host);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            #region 登录获取token

            string error = string.Empty;
            if (string.IsNullOrEmpty(tokenValue))
            {
                (tokenValue, error) = await MoxaLoginAsync(httpClient);
                context.JobDetail.JobDataMap.Put("token", tokenValue);                
            }
            if (string.IsNullOrEmpty(tokenValue))
            {
                return;
            }

            #endregion

            #region 获取Site ID
            moxaResp sitesResp = await GetJArrayAsync(httpClient, "/api/sites", tokenValue);
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
             设备断电类型：
                   65542 0x00010006  Warning 電源 x 斷電 Power x is down x: The nth power input(1 or 2)	{ trap_detail: x}
                   65543 0x00010007  Warning 設備無法以SNMP存取 Device SNMP unreachable

             设备上电类型：
                   2147549190    0x80010006  Normal(Information)    電源 x 通電 Power x is up x: The nth power input(1 or 2)	{ trap_detail: x}
                   2147549191    0x80010007  Normal(Information)    設備恢復以SNMP存取 Device SNMP reachable
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

            moxaResp respEvents = await GetJArrayAsync(httpClient, $"/api/events/site/{siteid}?count=100", tokenValue);
            if (!respEvents.ok) { return; }
            else
            {
                string[] conditions = ["65542", "65543", "2147549190", "2147549191"];
                var sortedEvents = from x in respEvents.data
                                   where x["type"] != null && conditions.Contains(x["type"]!.ToString().Trim())
                                   orderby long.Parse(x["event_time"]!.ToString()) descending
                                   select x;
                if (!sortedEvents.Any())
                {
                    //device is alright?
                    _logger.LogInformation("no device event observed");
                }
                else
                {
                    var firstOne = sortedEvents.First();
                    string ip = firstOne["source_ip"]!.ToString();
                    string device_id = firstOne["device_id"]!.ToString();
                    switch (firstOne["type"]!.ToString())
                    {
                        case "65542":
                        case "65543":
                            //断电
                            Console.WriteLine("{0} is offline", ip, device_id);
                            break;
                        case "2147549190":
                        case "2147549191":
                            //上电
                            Console.WriteLine("{0} is online", ip, device_id);
                            break;
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
                    return new moxaResp(resp.errno, resp.message, false, []);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("{0} FAILED, {1}", api, ex.StackTrace!.ToString());
            }
            return new moxaResp("", "", false, []);
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
}
