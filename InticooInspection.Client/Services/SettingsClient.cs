using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.SessionStorage;

namespace InticooInspection.Client.Services
{
    /// <summary>Đọc/ghi cài đặt toàn hệ thống (api/settings/{section}). Chỉ Admin.</summary>
    public class SettingsClient
    {
        public const string InspectionDefect = "inspection-defect";
        public const string SystemSection = "system";
        public const string Billing = "billing";

        private readonly HttpClient _http;
        private readonly ISessionStorageService _session;

        public SettingsClient(HttpClient http, ISessionStorageService session)
        {
            _http = http;
            _session = session;
        }

        private async Task AuthAsync()
        {
            var token = await _session.GetItemAsync<string>("auth_token");
            if (!string.IsNullOrEmpty(token))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>null khi chưa lưu lần nào (dùng giá trị mặc định) hoặc lỗi.</summary>
        public async Task<T?> LoadAsync<T>(string section) where T : class
        {
            try
            {
                await AuthAsync();
                var resp = await _http.GetAsync($"api/settings/{section}");
                if (resp.StatusCode == System.Net.HttpStatusCode.NoContent || !resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<T>();
            }
            catch { return null; }
        }

        public async Task<bool> SaveAsync<T>(string section, T model)
        {
            try
            {
                await AuthAsync();
                var resp = await _http.PutAsJsonAsync($"api/settings/{section}", model);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
