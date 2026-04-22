using System.Net.Http.Headers;
using System.Text.Json;
using Blazored.SessionStorage;

namespace InticooInspection.Client.Services
{
    public class PageAccessService
    {
        private readonly HttpClient _http;
        private readonly ISessionStorageService _session;

        private static readonly JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        private HashSet<string>? _cachedPages;
        private List<string>     _cachedRoles = new();
        private bool             _loaded      = false;
        private Task?            _inflight;

        public event Action? OnChange;

        public PageAccessService(HttpClient http, ISessionStorageService session)
        {
            _http    = http;
            _session = session;
        }

        public IReadOnlyCollection<string> Pages => _cachedPages ?? (IReadOnlyCollection<string>)Array.Empty<string>();
        public IReadOnlyCollection<string> Roles => _cachedRoles;
        public bool IsLoaded => _loaded;
        public bool IsAdmin  => _cachedRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

        public bool HasAccess(string pageKey)
        {
            if (!_loaded) return false;
            if (IsAdmin) return true;
            return _cachedPages != null && _cachedPages.Contains(pageKey);
        }

        public async Task EnsureLoadedAsync()
        {
            if (_loaded) return;
            if (_inflight != null) { await _inflight; return; }
            _inflight = LoadAsync();
            try { await _inflight; }
            finally { _inflight = null; }
        }

        public async Task ReloadAsync()
        {
            _loaded = false; _cachedPages = null; _cachedRoles = new();
            await EnsureLoadedAsync();
            OnChange?.Invoke();
        }

        public void Clear()
        {
            _loaded = false; _cachedPages = null; _cachedRoles = new();
            OnChange?.Invoke();
        }

        private async Task LoadAsync()
        {
            try
            {
                string? token = null;
                try
                {
                    token = await _session.GetItemAsync<string>("auth_token");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PageAccess] Read token error: {ex.Message}");
                }

                Console.WriteLine($"[PageAccess] Token present: {!string.IsNullOrEmpty(token)}");
                if (!string.IsNullOrEmpty(token))
                {
                    // Show token format — first 20 chars + length. Never log the whole thing.
                    var preview = token.Length > 40 ? token.Substring(0, 20) + "..." + token.Substring(token.Length - 10) : token;
                    Console.WriteLine($"[PageAccess] Token preview: {preview} (len={token.Length})");
                    // Check if the token has 3 dot-separated parts (valid JWT)
                    var parts = token.Split('.');
                    Console.WriteLine($"[PageAccess] Token JWT parts: {parts.Length} (should be 3)");
                }

                if (string.IsNullOrEmpty(token))
                {
                    _cachedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = new();
                    return;
                }

                // Build request with explicit Authorization header
                using var req = new HttpRequestMessage(HttpMethod.Get, "api/me/page-access");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                Console.WriteLine($"[PageAccess] Sending request to: {_http.BaseAddress}api/me/page-access");
                Console.WriteLine($"[PageAccess] Authorization header set: {req.Headers.Authorization}");

                using var response = await _http.SendAsync(req);
                Console.WriteLine($"[PageAccess] Status: {(int)response.StatusCode} {response.StatusCode}");

                var raw = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[PageAccess] Body: {raw}");

                if (!response.IsSuccessStatusCode)
                {
                    _cachedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = new();
                    return;
                }

                var dto = JsonSerializer.Deserialize<PageAccessDto>(raw, _jsonOpts);
                if (dto != null)
                {
                    _cachedPages = string.IsNullOrWhiteSpace(dto.PageAccess)
                        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(
                              dto.PageAccess.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(x => x.Trim()),
                              StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = dto.Roles ?? new();

                    Console.WriteLine($"[PageAccess] Loaded pages: [{string.Join(", ", _cachedPages)}]");
                    Console.WriteLine($"[PageAccess] Loaded roles: [{string.Join(", ", _cachedRoles)}]");
                }
                else
                {
                    _cachedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PageAccess] Exception: {ex.GetType().Name}: {ex.Message}");
                _cachedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _cachedRoles = new();
            }
            finally
            {
                _loaded = true;
                OnChange?.Invoke();
            }
        }

        private class PageAccessDto
        {
            public string?      PageAccess { get; set; }
            public List<string> Roles      { get; set; } = new();
        }
    }
}
