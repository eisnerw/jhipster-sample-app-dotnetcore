using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace JhipsterSampleApplication.Test.Helpers
{
    public static class EsAwait
    {
        public static async Task AwaitApiHitsAsync<T>(HttpClient client, string url, Func<T, int> getCount, int expected = 1, int timeoutMs = 2000, int stepMs = 100)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                var resp = await client.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var dto = JsonConvert.DeserializeObject<T>(json);
                    if (getCount(dto) >= expected) return;
                }
                await Task.Delay(stepMs);
            }
            throw new TimeoutException($"Expected {expected} hits at {url} within {timeoutMs}ms.");
        }
    }
}

