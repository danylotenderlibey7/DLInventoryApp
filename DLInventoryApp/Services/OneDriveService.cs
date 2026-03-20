using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services.Options;
using Microsoft.Extensions.Options;

namespace DLInventoryApp.Services
{

    public class OneDriveService : IOneDriveService
    {
        private readonly HttpClient _http;
        private readonly OneDriveOptions _cfg;
        public OneDriveService(HttpClient http, IOptions<OneDriveOptions> opts)
        {
            _http = http;
            _cfg = opts.Value;
        }
        public async Task UploadJsonAsync(string fileName, string jsonContent)
        {
            var accessToken = await GetAccessTokenAsync();
            var folder = _cfg.FolderPath.Trim('/');
            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{folder}/{fileName}:/content";
            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"OneDrive upload failed. Status: {(int)response.StatusCode}. {body}");
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _cfg.ClientId,
                ["refresh_token"] = _cfg.RefreshToken,
                ["scope"] = "Files.ReadWrite offline_access"
            });
            var response = await _http.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", body);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"OneDrive auth failed. Status: {(int)response.StatusCode}. {responseBody}");
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var accessToken = root.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Missing access_token in OneDrive response.");
            if (root.TryGetProperty("refresh_token", out var newRefresh))
                _cfg.RefreshToken = newRefresh.GetString()!;
            return accessToken;
        }
    }
}