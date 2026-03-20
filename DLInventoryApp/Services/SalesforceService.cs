using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services.Options;
using Microsoft.Extensions.Options;

namespace DLInventoryApp.Services
{
    public class SalesforceService : ISalesforceService
    {
        private const string ApiVersion = "v59.0";
        private readonly HttpClient _http;
        private readonly SalesforceSettings _cfg;
        public SalesforceService(HttpClient http, IOptions<SalesforceSettings> opts)
        {
            _http = http;
            _cfg = opts.Value;
        }
        public async Task ExportContactAsync(string firstName, string lastName, string email, 
            string phone, string companyName, string jobTitle)
        {
            var (token, instanceUrl) = await GetTokenAsync();
            var exists = await ContactExistsAsync(token, instanceUrl, email);
            if (exists) throw new InvalidOperationException("A contact with this email already exists.");
            var payload = new
            {
                allOrNone = true,
                compositeRequest = new object[]
                {
                    new
                    {
                        method = "POST",
                        url = $"/services/data/{ApiVersion}/sobjects/Account",
                        referenceId = "newAccount",
                        body = new { Name = companyName }
                    },
                    new
                    {
                        method = "POST",
                        url = $"/services/data/{ApiVersion}/sobjects/Contact",
                        referenceId = "newContact",
                        body = new
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            Email = email,
                            Phone = phone,
                            Title = jobTitle,
                            AccountId = "@{newAccount.id}"
                        }
                    }
                }
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{instanceUrl}/services/data/{ApiVersion}/composite");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Salesforce request failed. Status: {(int)resp.StatusCode}. {ParseError(body)}");
            ValidateCompositeResponse(body);
        }
        private async Task<(string token, string instanceUrl)> GetTokenAsync()
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _cfg.ClientId,
                ["client_secret"] = _cfg.ClientSecret,
                ["username"] = _cfg.Username,
                ["password"] = _cfg.Password
            });
            var resp = await _http.PostAsync($"{_cfg.LoginUrl}/services/oauth2/token", body);
            var responseBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Salesforce auth failed. Status: {(int)resp.StatusCode}. {ParseError(responseBody)}");
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            return (
                root.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Missing access_token."),
                root.GetProperty("instance_url").GetString() ?? throw new InvalidOperationException("Missing instance_url.")
            );
        }
        private static void ValidateCompositeResponse(string responseBody)
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("compositeResponse", out var responses)) return;
            foreach (var item in responses.EnumerateArray())
            {
                var status = item.TryGetProperty("httpStatusCode", out var s) ? s.GetInt32() : 200;
                if (status >= 200 && status < 300) continue;
                var details = item.TryGetProperty("body", out var b) ? ParseError(b.GetRawText()) : "Unknown error.";
                throw new InvalidOperationException($"Salesforce composite failed. Status: {status}. {details}");
            }
        }
        private async Task<bool> ContactExistsAsync(string token, string instanceUrl, string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var soql = $"SELECT Id FROM Contact WHERE Email = '{EscapeSoqlValue(email)}' LIMIT 1";
            var url = $"{instanceUrl}/services/data/{ApiVersion}/query?q={Uri.EscapeDataString(soql)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Salesforce query failed. Status: {(int)resp.StatusCode}. {ParseError(body)}");
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("totalSize", out var totalSize)) return false;
            return totalSize.GetInt32() > 0;
        }
        private static string ParseError(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "No details returned.";
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var first = root[0];
                    var code = first.TryGetProperty("errorCode", out var c) ? c.GetString() : null;
                    var msg = first.TryGetProperty("message", out var m) ? m.GetString() : null;
                    if (code != null || msg != null) return $"{code}: {msg}";
                }
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("error", out var err)
                    && root.TryGetProperty("error_description", out var desc))
                    return $"{err.GetString()}: {desc.GetString()}";
            }
            catch { }
            return json;
        }
        private static string EscapeSoqlValue(string value) =>
            value.Replace("\\", "\\\\").Replace("'", "\\'");
    }
}