using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace McmLib.Services
{
    public sealed class MailApiClient
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl = "http://php84.localhost/mcmapi/api/";
        private readonly string _apiKey = "8Tqk9Vn2LmR4xJ7cPd5Hs0Yw3Fb6Zu1Ne8Ka2Qm7Dx4Rc9Wp5Gj1Tv6By3Uh0Mz";
        public MailApiClient()
        {
            _http = new HttpClient();

            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            _http.DefaultRequestHeaders.Remove("X-API-Key");
            _http.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
        public MailApiClient(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient();

            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            _http.DefaultRequestHeaders.Remove("X-API-Key");
            _http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }

        public async Task<string> TestSmtpAsync()
        {
            var url = _baseUrl + "utilities/test_smtp.php";
            using var resp = await _http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception("Test SMTP failed: " + (int)resp.StatusCode + " " + body);

            return body;
        }

        public async Task<string> SendEmailAsync(string to, string subject, string htmlBody)
        {
            var url = _baseUrl + "utilities/send_email.php";

            var payload = new
            {
                to = to,
                subject = subject,
                body = htmlBody
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception("Send email failed: " + (int)resp.StatusCode + " " + body);

            return body;
        }
    }
}
