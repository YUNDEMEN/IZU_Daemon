using IZU.Entities;
using IZU.Interfaces;
using IZU.Service;
using System.Text.Json.Nodes;

namespace IZU.Base
{
    public static class Extensions
    {
        public static IServiceCollection AddIZU(this IServiceCollection service,IConfiguration config)
        {
            service.Configure<IZUConfig>(config);
            service.AddSingleton<IIZUService, IZUService>();
            service.AddSingleton<IS7NetService, S7NetService>();
            service.AddSingleton<IIZUBroadcastServer, IZUWebsocketServer>();
            return service;
        }
        public static async Task UseIZUAsync(this WebApplication app)
        {
            IIZUService? izuService = app.Services.GetService<IIZUService>();
            if (izuService == null)
                throw new Exception("should add izu first");
            await izuService.StartAsync();
            app.MapGet("/", () => izuService.ServiceRuntime);

            IIZUBroadcastServer? izuSock = app.Services.GetService<IIZUBroadcastServer>();
            if (izuService == null)
                throw new Exception("should add izu first");

            app.Map("/ws", config =>
            {
                config.UseWebSockets();
                config.Use(async (context, next) => await izuSock.Acceptor(context, next));
            });
        }


        public static async Task<Resp> HttpGetAsync(this string api, int timeoutSeconds = 5)
        {
            using HttpClient httpClient = new HttpClient();
            string error = string.Empty;
            string result=string.Empty;
            try
            {
                httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await httpClient.GetAsync(api);
                if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                {
                    result = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                error = $"Http Get Method Error: {api}, {ex.Message}";
            }
            finally
            {
                httpClient.Dispose();
            }
            return new Resp(result,error );
        }

        public static async Task<Resp> HttpPostAsync(this string api, int timeoutSeconds = 5)
        {
            using HttpClient httpClient = new HttpClient();
            string error = string.Empty;
            string result = string.Empty;
            try
            {
                httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await httpClient.GetAsync(api);
                if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
                {
                    result = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                error = $"Http Get Method Error: {api}, {ex.Message}";
            }
            finally
            {
                httpClient.Dispose();
            }
            return new Resp(result, error);
        }

        public static async Task<T?> HttpGetAsync<T>(this string api, int timeoutSeconds = 5)
        {
            Resp resp = await api.HttpGetAsync(timeoutSeconds);
            if (!string.IsNullOrWhiteSpace(resp.error))
            {
                throw new Exception($"upload izu info failed: {resp.error}");
            }
            else
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(resp.result);
            }
        }
    }
}
