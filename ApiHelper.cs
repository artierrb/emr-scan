using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EMRScan
{
    public static class ApiHelper
    {
        private static readonly HttpClient _http = new HttpClient();

        public static async Task<bool> NotifyScanComplete(string treatNo, string formCode, string userId)
        {
            try
            {
                string json = $"{{\"treatNo\":\"{treatNo}\",\"formCode\":\"{formCode}\",\"userId\":\"{userId}\"}}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _http.PostAsync(
                    $"{AppConfig.ApiBaseUrl}/api/scan/complete", content);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<(string userId, string name, string auth)?> VerifyToken(string token)
        {
            try
            {
                var res = await _http.GetAsync(
                    $"{AppConfig.ApiBaseUrl}/api/scan/verify?token={Uri.EscapeDataString(token)}");
                if (!res.IsSuccessStatusCode) return null;
                string body = await res.Content.ReadAsStringAsync();
                // simple JSON parse
                string userId = ExtractJson(body, "userId");
                string name   = ExtractJson(body, "name");
                string auth   = ExtractJson(body, "auth");
                return (userId, name, auth);
            }
            catch { return null; }
        }

        private static string ExtractJson(string json, string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                json, $"\"{key}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : "";
        }
    }
}
