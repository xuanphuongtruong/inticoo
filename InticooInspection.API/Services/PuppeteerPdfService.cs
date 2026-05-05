using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace InticooInspection.API.Services;

/// <summary>
/// Service xuất PDF server-side bằng Puppeteer Sharp (headless Chromium).
/// Render trang Blazor inspection-report sang PDF với chất lượng pixel-perfect.
///
/// Lợi ích so với window.print() client-side:
/// - Output GIỐNG NHAU 100% trên mọi máy user (không phụ thuộc browser/OS/driver Print)
/// - Footer/header chuẩn (dùng headerTemplate/footerTemplate của Puppeteer)
/// - Số trang tự động đúng (ko cần CSS counter hack)
/// - Không bị lỗi trùng footer / trang trắng / scale lệch trên Windows
/// - Nhúng được watermark, signature, metadata PDF
///
/// Setup:
/// 1. NuGet: Install-Package PuppeteerSharp
/// 2. Lần chạy đầu: PuppeteerSharp tự download Chromium (~200MB) vào
///    bin/.local-chromium. Có thể pre-download trong startup hoặc dùng
///    docker image có sẵn Chrome.
/// 3. Đăng ký service: builder.Services.AddScoped&lt;IPdfService, PuppeteerPdfService&gt;();
/// </summary>
public interface IPdfService
{
    Task<byte[]> GenerateInspectionReportPdfAsync(int inspectionId, string authToken, CancellationToken ct = default);
}

public class PuppeteerPdfService : IPdfService, IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<PuppeteerPdfService> _logger;
    private static IBrowser? _browser;
    private static readonly SemaphoreSlim _browserLock = new(1, 1);

    public PuppeteerPdfService(IConfiguration config, ILogger<PuppeteerPdfService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Render trang inspection-report của Blazor app sang PDF.
    /// </summary>
    /// <param name="inspectionId">ID của inspection cần xuất</param>
    /// <param name="authToken">Bearer token / session cookie để Blazor app
    /// gọi được API api/inspections/{id} (vì page có @inject HttpClient).</param>
    public async Task<byte[]> GenerateInspectionReportPdfAsync(
        int inspectionId, string authToken, CancellationToken ct = default)
    {
        var browser = await GetBrowserAsync();

        await using var page = await browser.NewPageAsync();

        // Set viewport theo desktop để tránh kích hoạt @media (max-width:860px) mobile
        await page.SetViewportAsync(new ViewPortOptions
        {
            Width  = 1280,
            Height = 1024,
            DeviceScaleFactor = 2  // retina, ảnh sắc nét hơn
        });

        // Forward auth token: set Authorization header cho mọi request HTTP của Puppeteer
        // (cần thiết khi app gọi API trực tiếp từ JS).
        if (!string.IsNullOrEmpty(authToken))
        {
            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {authToken}"
            });
        }

        // Build URL trỏ tới Blazor page.
        var baseUrl  = _config["AppBaseUrl"]?.TrimEnd('/')
                       ?? throw new InvalidOperationException(
                           "AppBaseUrl chưa được cấu hình trong appsettings.json");
        var pageUrl  = $"{baseUrl}/inspection-report/{inspectionId}?print=1";

        _logger.LogInformation("Rendering PDF for inspection {Id} from {Url}", inspectionId, pageUrl);

        // ─────────────────────────────────────────────────────────────
        // BƯỚC 1: Mở trang root trước (không yêu cầu auth) để có document
        // mà Puppeteer có thể inject sessionStorage vào.
        // Blazor app lưu JWT trong sessionStorage với key "auth_token".
        // Nếu navigate thẳng vào /inspection-report/{id} mà sessionStorage
        // trống → page guard redirect về login → không bao giờ thấy #rpt-root
        // → timeout 30s.
        // ─────────────────────────────────────────────────────────────
        await page.GoToAsync(baseUrl, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
            Timeout   = 30_000
        });

        // BƯỚC 2: Inject token vào sessionStorage (cùng key mà login flow lưu)
        if (!string.IsNullOrEmpty(authToken))
        {
            // Escape token để nhúng an toàn vào JS literal
            var escapedToken = System.Text.Json.JsonSerializer.Serialize(authToken);
            await page.EvaluateExpressionAsync($@"
                sessionStorage.setItem('auth_token', {escapedToken});
            ");
            _logger.LogInformation("Injected auth_token into sessionStorage");
        }

        // BƯỚC 3: Navigate vào trang inspection-report thật. Lúc này sessionStorage
        // đã có token → Blazor app load data + render bình thường.
        await page.GoToAsync(pageUrl, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
            Timeout   = 60_000
        });

        // Chờ thêm: đảm bảo Blazor đã render xong + tất cả ảnh đã decode
        // (Blazor render bất đồng bộ, networkidle không đủ để bắt sự kiện này)
        await page.WaitForFunctionAsync(@"
            () => {
                // Đợi Blazor render xong: element root tồn tại + không còn loading
                const root = document.getElementById('rpt-root');
                if (!root) return false;
                if (document.querySelector('.rpt-loading')) return false;

                // Đợi tất cả ảnh load xong
                const imgs = Array.from(document.querySelectorAll('img'));
                return imgs.every(img => img.complete && img.naturalHeight > 0);
            }
        ", new WaitForFunctionOptions { Timeout = 60_000 });

        // Inject CSS bổ sung khi render PDF (ẩn toolbar, đảm bảo desktop layout)
        await page.AddStyleTagAsync(new AddTagOptions
        {
            Content = @"
                /* Ẩn toolbar khi render PDF */
                .rpt-toolbar, .no-print { display: none !important; }
                /* Đảm bảo background trắng */
                html, body, .rpt-page { background: white !important; }
                /* Bỏ shadow + radius giống print mode */
                .rpt-section { box-shadow: none !important; border-radius: 0 !important; }
                /* Ẩn footer cũ trong section (sẽ dùng footerTemplate của Puppeteer) */
                .rpt-footer, .rpt-print-footer { display: none !important; }
            "
        });

        // Generate PDF với footer/header chuẩn từ Puppeteer
        // (thay thế hoàn toàn cơ chế position:fixed cũ — không còn lỗi trùng footer)
        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Format          = PaperFormat.A4,
            PrintBackground = true,
            DisplayHeaderFooter = true,
            MarginOptions = new MarginOptions
            {
                Top    = "15mm",
                Bottom = "28mm",  // chừa chỗ cho footer
                Left   = "0",
                Right  = "0"
            },
            HeaderTemplate = "<div></div>",  // không cần header
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

        _logger.LogInformation("PDF generated for inspection {Id}: {Size} bytes",
            inspectionId, pdfBytes.Length);

        return pdfBytes;
    }

    /// <summary>
    /// Lazy init browser. Dùng singleton browser cho cả app — tiết kiệm memory
    /// và khởi động nhanh. Mỗi request có Page riêng, không xung đột.
    /// </summary>
    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser != null && _browser.IsConnected) return _browser;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser != null && _browser.IsConnected) return _browser;

            // Download Chromium nếu chưa có (chỉ chạy lần đầu)
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
                    "--font-render-hinting=none"  // font sắc nét đều hơn
                }
            });

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
