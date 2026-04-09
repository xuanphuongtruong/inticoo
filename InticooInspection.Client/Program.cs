using Blazored.LocalStorage;
using Blazored.SessionStorage;
using InticooInspection.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<InticooInspection.Client.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

//builder.Services.AddSingleton(new HttpClient
//{
//    BaseAddress = new Uri("https://inticoo-e4hrerayb4b7h4fk.southeastasia-01.azurewebsites.net/")
//});

// Trong Program.cs của dự án Client
//string apiBaseUrl = builder.HostEnvironment.IsDevelopment()
//    ? "https://inticoo-e4hrerayb4b7h4fk.southeastasia-01.azurewebsites.net/" // Local gọi thẳng API
//    : "https://black-grass-002608310.2.azurestaticapps.net/"; // Production dùng Proxy của Azure

//builder.Services.AddSingleton(new HttpClient
//{
//    BaseAddress = new Uri(apiBaseUrl)
//});

// Tự động nhận diện môi trường
//var apiBaseUrl = builder.HostEnvironment.IsDevelopment()
//    ? "http://localhost:5034" // Khi đã deploy lên cùng một host (Proxy)
//    : "https://inticoo-e4hrerayb4b7h4fk.southeastasia-01.azurewebsites.net/"; // Khi dev ở máy

//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
var baseApiUrl = builder.Configuration["ApiBaseUrl"];
Console.WriteLine($"[CONFIG] Environment: {builder.HostEnvironment.Environment}");
Console.WriteLine($"[CONFIG] ApiBaseUrl: {baseApiUrl}");

// Nếu không tìm thấy cấu hình (ví dụ khi deploy lên SWA), mặc định dùng host hiện tại
if (string.IsNullOrEmpty(baseApiUrl))
{
    baseApiUrl = builder.HostEnvironment.BaseAddress;
}

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(baseApiUrl)
});

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredSessionStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthStateProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<WaitingService>();

await builder.Build().RunAsync();