using System.Text.Json.Serialization;
using System.Text.Json;

namespace Wonder.Infrastructure
{
    public record Resp(string result, string error);
    public static class HttpRequstExtensions
    {
        public static async Task<Resp> HttpGetAsync(this string api, int timeoutSeconds = 5)
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
