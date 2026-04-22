using System.Net.Http.Headers;
using System.Text.Json;
using Blazored.SessionStorage;

namespace InticooInspection.Client.Services
{
    /// <summary>
    /// Loads and caches the current user's Page Access list (CSV of page keys).
    /// Used by MainLayout to hide menu items and by PageAccessGuard to block routes.
    ///
    /// BACKEND CONTRACT:
    ///   GET /api/users/me/page-access → { pageAccess: "JobRequest,SummaryInspections", roles: ["Customer"] }
    ///
    /// Admin role bypasses all checks (HasAccess always returns true for Admin).
    /// </summary>
    public class PageAccessService
    {
        private readonly HttpClient _http;
        private readonly ISessionStorageService _session;

        // Case-insensitive JSON options so we accept both pageAccess and PageAccess
        private static readonly JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        private HashSet<string>? _cachedPages;   // null = not loaded yet
        private List<string>     _cachedRoles = new();
        private bool             _loaded      = false;
        private Task?            _inflight;    // dedupe concurrent loads

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
            _loaded      = false;
            _cachedPages = null;
            _cachedRoles = new();
            await EnsureLoadedAsync();
            OnChange?.Invoke();
        }

        public void Clear()
        {
            _loaded      = false;
            _cachedPages = null;
            _cachedRoles = new();
            OnChange?.Invoke();
        }

        private async Task LoadAsync()
        {
            try
            {
                // Always read token fresh and attach explicitly per-request.
                // Do NOT rely on HttpClient.DefaultRequestHeaders because:
                //   1) Another page may have set it to a different value
                //   2) On first MainLayout render, other pages haven't run yet
                string? token = null;
                try
                {
                    token = await _session.GetItemAsync<string>("auth_token");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PageAccess] Failed to read token from session: {ex.Message}");
                }

                Console.WriteLine($"[PageAccess] Token present: {!string.IsNullOrEmpty(token)}");
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("[PageAccess] No token, aborting load. User not logged in yet?");
                    _cachedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = new();
                    return;
                }

                using var req = new HttpRequestMessage(HttpMethod.Get, "api/users/me/page-access");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _http.SendAsync(req);
                Console.WriteLine($"[PageAccess] Status: {(int)response.StatusCode} {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[PageAccess] Error body: {body}");
                    _cachedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = new();
                    return;
                }

                var raw = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[PageAccess] Raw response: {raw}");

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
                    Console.WriteLine($"[PageAccess] IsAdmin: {IsAdmin}");
                }
                else
                {
                    Console.WriteLine("[PageAccess] DTO was null after deserialize");
                    _cachedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PageAccess] Exception: {ex.Message}");
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
