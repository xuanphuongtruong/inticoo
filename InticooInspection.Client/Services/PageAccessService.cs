using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        public bool IsAdmin => _cachedRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns true if the user is an Admin (bypass), OR the given page key
        /// is in the user's page access list. If access list has not been loaded
        /// yet, returns false — callers should await EnsureLoadedAsync first.
        /// </summary>
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
                // Make sure Authorization header is set (in case consumer hasn't)
                if (_http.DefaultRequestHeaders.Authorization == null)
                {
                    try
                    {
                        var token = await _session.GetItemAsync<string>("auth_token");
                        if (!string.IsNullOrEmpty(token))
                            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    }
                    catch { /* ignore */ }
                }

                var dto = await _http.GetFromJsonAsync<PageAccessDto>("api/users/me/page-access");
                if (dto != null)
                {
                    _cachedPages = string.IsNullOrWhiteSpace(dto.PageAccess)
                        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(
                              dto.PageAccess.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(x => x.Trim()),
                              StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = dto.Roles ?? new();
                }
                else
                {
                    _cachedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _cachedRoles = new();
                }
            }
            catch
            {
                // On failure, default to empty access. Admin claims will still come
                // from JWT/AuthorizeView so core admin UI keeps working, but
                // page-access-gated items will be hidden until a successful load.
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
