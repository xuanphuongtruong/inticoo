using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace InticooInspection.Client.Services
{
    public class LoginDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }

    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Message { get; set; }
        public UserInfoDto? User { get; set; }
    }

    public class UserInfoDto
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "User";
    }

    public class DashboardDto
    {
        public int TotalInspections { get; set; }
        public int PendingInspections { get; set; }
        public int InProgressInspections { get; set; }
        public int CompletedInspections { get; set; }
        public double CompletionRate { get; set; }
        public List<InspectionDto> RecentInspections { get; set; } = new();
    }

    public class InspectionDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; } = "";
        public int TotalSteps { get; set; }
        public int CompletedSteps { get; set; }
    }

    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task LogoutAsync();
        Task<UserInfoDto?> GetCurrentUserAsync();
    }

    public class AuthService : IAuthService
    {
        private readonly HttpClient _http;
        private readonly ISessionStorageService _localStorage;
        private readonly AuthenticationStateProvider _authStateProvider;

        public AuthService(HttpClient http, ISessionStorageService localStorage,
                           AuthenticationStateProvider authStateProvider)
        {
            _http = http;
            _localStorage = localStorage;
            _authStateProvider = authStateProvider;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", dto);
            var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

            if (result?.Success == true && result.Token != null)
            {
                await _localStorage.SetItemAsync("auth_token", result.Token);
                await _localStorage.SetItemAsync("user_info", result.User);
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", result.Token);
                ((JwtAuthStateProvider)_authStateProvider).NotifyAuthStateChanged();
            }

            return result ?? new AuthResponseDto { Success = false, Message = "Unknown error." };
        }

        public async Task LogoutAsync()
        {
            try { await _http.PostAsync("api/auth/logout", null); } catch { }
            await _localStorage.RemoveItemAsync("auth_token");
            await _localStorage.RemoveItemAsync("user_info");
            _http.DefaultRequestHeaders.Authorization = null;
            ((JwtAuthStateProvider)_authStateProvider).NotifyAuthStateChanged();
        }

        public async Task<UserInfoDto?> GetCurrentUserAsync()
            => await _localStorage.GetItemAsync<UserInfoDto>("user_info");
    }

    public class JwtAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ISessionStorageService _localStorage;
        private readonly HttpClient _http;

        public JwtAuthStateProvider(ISessionStorageService localStorage, HttpClient http)
        {
            _localStorage = localStorage;
            _http = http;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _localStorage.GetItemAsync<string>("auth_token");

            if (string.IsNullOrWhiteSpace(token))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            try
            {
                var claims = ParseClaimsFromJwt(token);
                var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
                if (expClaim != null && long.TryParse(expClaim.Value, out long expSeconds))
                {
                    var expTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                    if (expTime < DateTimeOffset.UtcNow)
                    {
                        await _localStorage.RemoveItemAsync("auth_token");
                        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                    }
                }

                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                return new AuthenticationState(
                    new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt")));
            }
            catch
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }

        public void NotifyAuthStateChanged()
            => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

        private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return Enumerable.Empty<Claim>();

            var payload = parts[1];
            var remainder = payload.Length % 4;
            if (remainder == 2) payload += "==";
            else if (remainder == 3) payload += "=";

            var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bytes);
            if (json == null) return Enumerable.Empty<Claim>();

            var claims = new List<Claim>();
            foreach (var kvp in json)
            {
                var type = kvp.Key switch
                {
                    "sub"   => ClaimTypes.NameIdentifier,
                    "email" => ClaimTypes.Email,
                    "role"  => ClaimTypes.Role,
                    _       => kvp.Key
                };
                var value = kvp.Value.ValueKind == JsonValueKind.String
                    ? kvp.Value.GetString() ?? ""
                    : kvp.Value.ToString();
                claims.Add(new Claim(type, value));
            }
            return claims;
        }
    }
}
