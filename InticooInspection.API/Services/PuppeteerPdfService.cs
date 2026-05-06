using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace InticooInspection.API.Services;

/// <summary>
/// Service xuất PDF server-side bằng Puppeteer Sharp (headless Chromium).
/// Render trang Blazor inspection-report sang PDF.
///
/// THAY ĐỔI v2 (so với v1):
/// 1. Dùng EvaluateFunctionOnNewDocumentAsync để inject auth_token TRƯỚC khi
///    page load → đảm bảo Blazor đọc được token ngay lần đầu khởi tạo HttpClient.
/// 2. Bỏ bước "GoToAsync(baseUrl) rồi mới GoToAsync(pageUrl)" — chỉ navigate 1 lần.
/// 3. Bỏ footer template Puppeteer (DisplayHeaderFooter=false) → dùng footer
///    sẵn có của Razor view (.rpt-print-footer) để tránh double footer.
/// 4. Margin bottom = 0 (để footer fixed của view không bị đè).
/// </summary>
public interface IPdfService
{
    Task<byte[]> GenerateInspectionReportPdfAsync(int inspectionId, string authToken, CancellationToken ct = default);
}

public class PuppeteerPdfService : IPdfService, IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<PuppeteerPdfService> _logger;
    private readonly IWebHostEnvironment _env;
    private static IBrowser? _browser;
    private static readonly SemaphoreSlim _browserLock = new(1, 1);

    public PuppeteerPdfService(
        IConfiguration config,
        ILogger<PuppeteerPdfService> logger,
        IWebHostEnvironment env)
    {
        _config = config;
        _logger = logger;
        _env    = env;
    }

    public async Task<byte[]> GenerateInspectionReportPdfAsync(
        int inspectionId, string authToken, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("════════════════════════════════════════════════════");
        _logger.LogInformation("[PDF] Starting generation for inspection {Id}", inspectionId);

        if (string.IsNullOrEmpty(authToken))
        {
            throw new UnauthorizedAccessException(
                "Auth token is required. Frontend must forward Authorization header.");
        }

        var browser = await GetBrowserAsync();
        await using var page = await browser.NewPageAsync();

        // ─────────────────────────────────────────────────────────────
        // Hook console + error events từ browser → log ASP.NET
        // ─────────────────────────────────────────────────────────────
        page.Console += (sender, e) =>
        {
            var type = e.Message.Type.ToString().ToUpper();
            _logger.LogInformation("[PDF/browser/{Type}] {Text}", type, e.Message.Text);
        };
        page.PageError += (sender, e) =>
        {
            _logger.LogError("[PDF/browser/PAGEERROR] {Message}", e.Message);
        };
        page.RequestFailed += (sender, e) =>
        {
            _logger.LogWarning("[PDF/browser/REQFAILED] {Url} → {Error}",
                e.Request.Url, e.Request.FailureText);
        };
        page.Response += (sender, e) =>
        {
            if ((int)e.Response.Status >= 400)
            {
                _logger.LogWarning("[PDF/browser/RESP] {Status} {Url}",
                    (int)e.Response.Status, e.Response.Url);
            }
        };

        // Viewport desktop để tránh @media (max-width:860px) mobile
        await page.SetViewportAsync(new ViewPortOptions
        {
            Width  = 1280,
            Height = 1024,
            DeviceScaleFactor = 2
        });

        var baseUrl = _config["AppBaseUrl"]?.TrimEnd('/')
                       ?? throw new InvalidOperationException(
                           "AppBaseUrl chưa được cấu hình trong appsettings.json");
        var pageUrl = $"{baseUrl}/inspection-report/{inspectionId}?print=1";

        _logger.LogInformation("[PDF] Base URL: {BaseUrl}", baseUrl);
        _logger.LogInformation("[PDF] Target page: {Url}", pageUrl);

        try
        {
            // ─────────────────────────────────────────────────────────
            // CRITICAL: Inject auth_token vào sessionStorage TRƯỚC khi
            // page load. EvaluateFunctionOnNewDocumentAsync sẽ chạy
            // script TRƯỚC bất kỳ JS nào của trang → đảm bảo Blazor
            // WASM đọc được token ngay khi khởi tạo.
            // ─────────────────────────────────────────────────────────
            var escapedToken = System.Text.Json.JsonSerializer.Serialize(authToken);
            _logger.LogInformation("[PDF] Step 1/3: Injecting auth_token before page load (length={Len})...",
                authToken.Length);

            await page.EvaluateFunctionOnNewDocumentAsync($@"
                () => {{
                    try {{
                        sessionStorage.setItem('auth_token', {escapedToken});
                        // Một số app dùng localStorage làm fallback
                        localStorage.setItem('auth_token', {escapedToken});
                        console.log('[PDF-INJECT] Token injected, length=' + {escapedToken}.length);
                    }} catch (e) {{
                        console.error('[PDF-INJECT] Failed:', e.message);
                    }}
                }}
            ");

            // ─────────────────────────────────────────────────────────
            // Step 2: Navigate trực tiếp tới trang report
            // ─────────────────────────────────────────────────────────
            _logger.LogInformation("[PDF] Step 2/3: Navigating to {Url}...", pageUrl);
            await page.GoToAsync(pageUrl, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Timeout   = 90_000
            });
            _logger.LogInformation("[PDF] Step 2/3: ✓ Navigation complete ({Ms}ms)", sw.ElapsedMilliseconds);

            // ─────────────────────────────────────────────────────────
            // Step 3: Đợi Blazor render xong + ảnh load + KHÔNG có error/login
            // ─────────────────────────────────────────────────────────
            _logger.LogInformation("[PDF] Step 3/3: Waiting for #rpt-root + images...");

            try
            {
                await page.WaitForFunctionAsync(@"
                    () => {
                        // Detect lỗi rõ ràng để fail nhanh
                        const errorEl = document.querySelector('.rpt-error');
                        if (errorEl) {
                            throw new Error('Blazor app shows error: ' + errorEl.textContent.trim());
                        }
                        // Detect bị redirect sang login
                        if (window.location.pathname.toLowerCase().includes('login')) {
                            throw new Error('Redirected to login: ' + window.location.href);
                        }
                        // Đợi root + hết loading
                        const root = document.getElementById('rpt-root');
                        if (!root) return false;
                        if (document.querySelector('.rpt-loading')) return false;
                        // Đợi tất cả ảnh load xong
                        const imgs = Array.from(document.querySelectorAll('#rpt-root img'));
                        return imgs.every(img => !img.src || (img.complete && img.naturalHeight > 0));
                    }
                ", new WaitForFunctionOptions { Timeout = 90_000 });

                _logger.LogInformation("[PDF] Step 3/3: ✓ Page rendered ({Ms}ms)", sw.ElapsedMilliseconds);
            }
            catch (Exception waitEx)
            {
                _logger.LogError("[PDF] Step 3/3: ✗ Wait failed — capturing diagnostics...");
                await CaptureDebugInfoAsync(page, inspectionId);
                throw new InvalidOperationException(
                    $"Puppeteer timeout while waiting for #rpt-root. " +
                    $"See screenshot + HTML dump in {GetDebugDir()}. " +
                    $"Original error: {waitEx.Message}", waitEx);
            }

            // Đợi font load
            try
            {
                await page.EvaluateExpressionAsync("document.fonts && document.fonts.ready");
            }
            catch { /* ignore */ }

            // ─────────────────────────────────────────────────────────
            // Generate PDF — KHÔNG dùng Puppeteer header/footer (dùng
            // footer sẵn có của Razor view để tránh double).
            // Margin = 0 để view tự kiểm soát layout.
            // ─────────────────────────────────────────────────────────
            _logger.LogInformation("[PDF] Generating PDF binary...");
            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format              = PaperFormat.A4,
                PrintBackground     = true,
                PreferCSSPageSize   = true,
                DisplayHeaderFooter = false,
                MarginOptions       = new MarginOptions
                {
                    Top    = "0",
                    Bottom = "0",
                    Left   = "0",
                    Right  = "0"
                }
            });

            sw.Stop();
            _logger.LogInformation("[PDF] ✓ Done. {Size} bytes in {Ms}ms",
                pdfBytes.Length, sw.ElapsedMilliseconds);
            _logger.LogInformation("════════════════════════════════════════════════════");

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PDF] ✗ Failed to generate PDF for inspection {Id}", inspectionId);
            throw;
        }
    }

    /// <summary>
    /// Khi Puppeteer timeout: chụp screenshot + dump HTML để inspect lỗi.
    /// </summary>
    private async Task CaptureDebugInfoAsync(IPage page, int inspectionId)
    {
        try
        {
            var debugDir = GetDebugDir();
            Directory.CreateDirectory(debugDir);

            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var pngPath  = Path.Combine(debugDir, $"insp-{inspectionId}-{ts}.png");
            var htmlPath = Path.Combine(debugDir, $"insp-{inspectionId}-{ts}.html");

            await page.ScreenshotAsync(pngPath, new ScreenshotOptions { FullPage = true });

            var html = await page.GetContentAsync();
            await File.WriteAllTextAsync(htmlPath, html);

            var currentUrl   = page.Url;
            var currentTitle = await page.GetTitleAsync();

            _logger.LogError("[PDF/DEBUG] Current URL: {Url}", currentUrl);
            _logger.LogError("[PDF/DEBUG] Page title: {Title}", currentTitle);
            _logger.LogError("[PDF/DEBUG] Screenshot saved: {Path}", pngPath);
            _logger.LogError("[PDF/DEBUG] HTML dump saved: {Path}", htmlPath);
        }
        catch (Exception captureEx)
        {
            _logger.LogError(captureEx, "[PDF/DEBUG] Failed to capture debug info");
        }
    }

    private string GetDebugDir() =>
        Path.Combine(_env.ContentRootPath, "wwwroot", "pdf-debug");

    /// <summary>
    /// Lazy-init Chromium browser. Singleton cho cả app.
    /// </summary>
    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser != null && _browser.IsConnected) return _browser;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser != null && _browser.IsConnected) return _browser;

            _logger.LogInformation("[PDF] Initializing Chromium (downloading if needed)...");
            await new BrowserFetcher().DownloadAsync();

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                    "--font-render-hinting=none"
                }
            });

            _logger.LogInformation("[PDF] ✓ Chromium ready");
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            await _browser.DisposeAsync();
            _browser = null;
        }
    }
}
