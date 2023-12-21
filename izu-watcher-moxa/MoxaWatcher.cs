using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace izu.watcher.moxa
{
    public interface IMoxaWatcher
    {
        void Run();
    }
    /*
     * 
     * 
     * /api/siteEvents/site/{siteId}                               Get {count} recent site events
     * /api/sites                                                           Get all unhidden sites
     * /api/sites/online                                                Get all online sites
     * 
     * 
     * /api/events/site/{siteId}                                     Get {count} recent events   query :  count  severity  acked
     * /api/events/site/{siteId}/type/{type}                  Get {type} events from a site.    
     * /api/events/type/{type}                                     Get {type} events from all sites.
     * /api/events/site/{siteId}/current                         Get all events which is currently triggered from a site.
     * /api/events/site/{siteId}/type/{type}/current      Get {type} events which is currently triggered from a site.
     */
    public class MoxaWatcher : IMoxaWatcher
    {
        readonly ILogger<MoxaWatcher> _logger;

        public MoxaWatcher(ILogger<MoxaWatcher> logger)
        {
            _logger = logger;
        }

        public void Run()
        {
            _logger.LogInformation("Moxa Watcher running");
            Task.Factory.StartNew(async () =>
            {
                (string tokenValue, string e) = await MoxaLoginAsync();
                if (string.IsNullOrEmpty(tokenValue))
                {
                    _logger.LogInformation("login failed : {0}", e);
                    return;
                }
                await GetSiteEventsAsync(tokenValue);
            });
        }

        async Task<(string,string)> MoxaLoginAsync()
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    httpClient.BaseAddress = new Uri("http://127.0.0.1");
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    HttpResponseMessage response = await httpClient.PostAsync($"login", JsonContent.Create(new
                    {
                        username = "admin",
                        password = "moxa"
                    }));
                    if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        var resultObject = JObject.Parse(result);
                        return (resultObject["mxviewGateway"].ToString(), string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    string err = $"login failed: {ex.Message}";
                    _logger.LogInformation(err);
                    return (string.Empty, err);
                }
                return (string.Empty, "some reason");
            }
        }


        async Task GetSiteEventsAsync(string tokenValue)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    //http://127.0.0.1/api/events/site/84f16950dfa6386b?count=50
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    httpClient.BaseAddress = new Uri("http://127.0.0.1");
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
                    HttpResponseMessage response = await httpClient.GetAsync($"api/events/site/84f16950dfa6386b?count=50");
                    if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        var resultObject = JArray.Parse(result);
                        Console.WriteLine(resultObject);
                    }
                }
                catch (Exception ex)
                {
                    string err = $"get failed: {ex.Message}";
                    _logger.LogInformation(err);
                }
            }
        }
    }
}
