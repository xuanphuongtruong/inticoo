using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace InticooInspection.API.Services;

/// <summary>
/// Service xuất PDF server-side bằng Puppeteer Sharp (headless Chromium).
/// Render trang Blazor inspection-report sang PDF với chất lượng pixel-perfect.
///
/// Phiên bản này có debug logging chi tiết để chẩn đoán lỗi timeout:
/// - Log từng bước (navigate → inject token → wait #rpt-root)
/// - Hook console của Puppeteer browser → đẩy log ra ASP.NET logger
/// - Khi timeout: chụp screenshot + dump HTML vào /wwwroot/pdf-debug để inspect
/// - Set timeout dài hơn (Blazor WASM lần đầu load mất 10-30s)
///
/// Setup:
/// 1. NuGet: dotnet add package PuppeteerSharp
/// 2. Lần chạy đầu tự download Chromium ~200MB
/// 3. Đăng ký service: builder.Services.AddScoped&lt;IPdfService, PuppeteerPdfService&gt;();
/// 4. appsettings.json: "AppBaseUrl": "http://localhost:5186" (URL của Blazor Client)
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

        var browser = await GetBrowserAsync();
        await using var page = await browser.NewPageAsync();

        // ─────────────────────────────────────────────────────────────
        // Hook console của browser → log ra ASP.NET (giúp debug Blazor app
        // báo gì khi load data — ví dụ "Failed to fetch" / "401" / etc.)
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
            // Chỉ log response có status >= 400 để bớt noise
            if ((int)e.Response.Status >= 400)
            {
                _logger.LogWarning("[PDF/browser/RESP] {Status} {Url}",
                    (int)e.Response.Status, e.Response.Url);
            }
        };

        // Set viewport desktop để tránh kích hoạt @media (max-width:860px) mobile
        await page.SetViewportAsync(new ViewPortOptions
        {
            Width  = 1280,
            Height = 1024,
            DeviceScaleFactor = 2
        });

        // Build URL Blazor app
        var baseUrl = _config["AppBaseUrl"]?.TrimEnd('/')
                       ?? throw new InvalidOperationException(
                           "AppBaseUrl chưa được cấu hình trong appsettings.json");
        var pageUrl = $"{baseUrl}/inspection-report/{inspectionId}?print=1";

        _logger.LogInformation("[PDF] Base URL: {BaseUrl}", baseUrl);
        _logger.LogInformation("[PDF] Target page: {Url}", pageUrl);

        try
        {
            // ─────────────────────────────────────────────────────────
            // BƯỚC 1: Mở trang root để có document → có thể access sessionStorage
            // ─────────────────────────────────────────────────────────
            _logger.LogInformation("[PDF] Step 1/4: Loading root page to access sessionStorage...");
            await page.GoToAsync(baseUrl, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                Timeout   = 60_000
            });
            _logger.LogInformation("[PDF] Step 1/4: ✓ Root page loaded ({Ms}ms)", sw.ElapsedMilliseconds);

            // ─────────────────────────────────────────────────────────
            // BƯỚC 2: Inject token vào sessionStorage
            // (Blazor Client đọc token từ sessionStorage["auth_token"]
            //  để gắn header Authorization khi gọi API)
            // ─────────────────────────────────────────────────────────
            if (!string.IsNullOrEmpty(authToken))
            {
                _logger.LogInformation("[PDF] Step 2/4: Injecting auth_token into sessionStorage (length={Len})...",
                    authToken.Length);

                var escapedToken = System.Text.Json.JsonSerializer.Serialize(authToken);
                await page.EvaluateExpressionAsync(
                    $"sessionStorage.setItem('auth_token', {escapedToken});");

                // Verify đã set thành công
                var verifyToken = await page.EvaluateExpressionAsync<string?>(
                    "sessionStorage.getItem('auth_token')");
                _logger.LogInformation("[PDF] Step 2/4: ✓ Token injected (verified length={Len})",
                    verifyToken?.Length ?? 0);
            }
            else
            {
                _logger.LogWarning("[PDF] Step 2/4: ⚠ No authToken provided — Blazor app may fail to load data");
            }

            // ─────────────────────────────────────────────────────────
            // BƯỚC 3: Navigate vào trang inspection-report thật
            // ─────────────────────────────────────────────────────────
            _logger.LogInformation("[PDF] Step 3/4: Navigating to {Url}...", pageUrl);
            await page.GoToAsync(pageUrl, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Timeout   = 90_000  // Blazor WASM lần đầu load mất 10-30s
            });
            _logger.LogInformation("[PDF] Step 3/4: ✓ Navigation complete ({Ms}ms)", sw.ElapsedMilliseconds);

            // ─────────────────────────────────────────────────────────
            // BƯỚC 4: Chờ Blazor render xong + ảnh load xong
            // ─────────────────────────────────────────────────────────
            _logger.LogInformation("[PDF] Step 4/4: Waiting for #rpt-root + images...");

            try
            {
                await page.WaitForFunctionAsync(@"
                    () => {
                        // Detect lỗi rõ ràng để fail nhanh thay vì chờ timeout
                        const errorEl = document.querySelector('.rpt-error');
                        if (errorEl) {
                            throw new Error('Blazor app shows error: ' + errorEl.textContent.trim());
                        }

                        // Detect đang ở trang login
                        if (window.location.pathname.toLowerCase().includes('login')) {
                            throw new Error('Redirected to login: ' + window.location.href);
                        }

                        // Đợi root + không còn loading
                        const root = document.getElementById('rpt-root');
                        if (!root) return false;
                        if (document.querySelector('.rpt-loading')) return false;

                        // Đợi tất cả ảnh load xong (ngoại trừ ảnh không có src)
                        const imgs = Array.from(document.querySelectorAll('#rpt-root img'));
                        return imgs.every(img => !img.src || (img.complete && img.naturalHeight > 0));
                    }
                ", new WaitForFunctionOptions { Timeout = 90_000 });

                _logger.LogInformation("[PDF] Step 4/4: ✓ Page rendered ({Ms}ms)", sw.ElapsedMilliseconds);
            }
            catch (Exception waitEx)
            {
                _logger.LogError("[PDF] Step 4/4: ✗ Wait failed — capturing diagnostics...");
                await CaptureDebugInfoAsync(page, inspectionId);
                throw new InvalidOperationException(
                    $"Puppeteer timeout while waiting for #rpt-root. " +
                    $"See screenshot + HTML dump in {GetDebugDir()}. " +
                    $"Original error: {waitEx.Message}", waitEx);
            }

            // ─────────────────────────────────────────────────────────
            // Inject CSS bổ sung khi render PDF
            // ─────────────────────────────────────────────────────────
            await page.AddStyleTagAsync(new AddTagOptions
            {
                Content = @"
                    .rpt-toolbar, .no-print { display: none !important; }
                    html, body, .rpt-page { background: white !important; }
                    .rpt-section { box-shadow: none !important; border-radius: 0 !important; }
                    .rpt-footer, .rpt-print-footer { display: none !important; }
                "
            });

            // ─────────────────────────────────────────────────────────
            // Generate PDF
            // ─────────────────────────────────────────────────────────
            _logger.LogInformation("[PDF] Generating PDF binary...");
            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format          = PaperFormat.A4,
                PrintBackground = true,
                DisplayHeaderFooter = true,
                MarginOptions = new MarginOptions
                {
                    Top    = "15mm",
                    Bottom = "28mm",
                    Left   = "0",
                    Right  = "0"
                },
                HeaderTemplate = "<div></div>",
                FooterTemplate = @"
                    <div style='font-family: Arial, sans-serif; font-size: 9px; color: #555;
                                width: 100%; padding: 0 12mm; box-sizing: border-box;
                                border-top: 1px solid #e8edf5; padding-top: 6px;'>
                        <div style='text-align: center; line-height: 1.4;'>
                            www.inticoo.com<br/>
                            The results presented herein reflect our findings at the time and place of inspection.
                            This report does not release the seller or manufacturer from their contractual obligations,
                            nor does it prejudice the buyer's right to seek compensation for any apparent or latent
                            defects not identified during our random inspection or arising thereafter.
                            This document does not constitute evidence of shipment.
                            Final approval from the client is required prior to the release of the goods.
                        </div>
                        <div style='position: absolute; right: 12mm; bottom: 8px;
                                    font-weight: 700; color: #888;'>
                            <span class='pageNumber'></span>
                        </div>
                    </div>"
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
    /// File lưu vào {ContentRoot}/wwwroot/pdf-debug/.
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

            // Screenshot trang đầy đủ (giúp thấy có trang login / error / blank không)
            await page.ScreenshotAsync(pngPath, new ScreenshotOptions { FullPage = true });

            // Dump HTML để có thể search element bằng tay
            var html = await page.GetContentAsync();
            await File.WriteAllTextAsync(htmlPath, html);

            // Log thêm URL hiện tại + title
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
    /// Lazy-init Chromium browser. Singleton cho cả app — tránh khởi động lại.
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
