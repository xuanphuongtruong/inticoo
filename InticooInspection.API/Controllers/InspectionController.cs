using InticooInspection.Domain.Security;
using InticooInspection.API.Services;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Mail;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/inspections")]
    [Authorize]
    public class InspectionController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;

        public InspectionController(AppDbContext db, UserManager<AppUser> userManager, IConfiguration config, IServiceScopeFactory scopeFactory)
        {
            _db = db;
            _userManager = userManager;
            _config = config;
            _scopeFactory = scopeFactory;
        }

        // GET api/inspections
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
            [FromQuery] string? customerId = null, [FromQuery] string? vendorId = null, [FromQuery] string? inspectionType = null)
        {
            try
            {
                // ── Dùng raw SQL-safe projection, không động đến navigation props ──
                var allRaw = await _db.Inspections
                    .IgnoreQueryFilters()   // danh sách Summary hiện cả job Draft để quản lý mở tiếp
                    .AsNoTracking()
                    .Select(i => new
                    {
                        i.Id,
                        i.Title,
                        StatusVal = i.Status,
                        InspectionTypeVal = i.InspectionType,
                        i.CreatedAt,
                        i.CompletedAt,
                        i.JobNumber,
                        i.InspectionDate,
                        i.CustomerName,
                        i.CustomerId,
                        i.VendorName,
                        i.VendorId,
                        i.ProductCategory,
                        i.ProductName,
                        i.ItemNumber,
                        i.InspectorId,
                        i.InspectorName,
                        i.PoNumber,
                        i.FinalResult,
                    })
                    .ToListAsync();

                // ── Phạm vi khách hàng của người gọi (Customer Access trên màn User) ──
                var custScope = await HttpContext.RequestServices.GetRequiredService<UserAccess>().CustomerScopeAsync();
                if (custScope != null)
                    allRaw = allRaw.Where(i => !string.IsNullOrEmpty(i.CustomerId) && custScope.Contains(i.CustomerId)).ToList();

                // Số liệu KPI (không phụ thuộc bộ lọc đang chọn)
                var kpiToday = DateTime.Today;
                var cntAll       = allRaw.Count(i => i.StatusVal != InspectionStatus.Draft);
                var cntCompleted = allRaw.Count(i => (int)i.StatusVal == 2);
                var cntCancel    = allRaw.Count(i => (int)i.StatusVal == 3);
                var cntDelay     = allRaw.Count(i => (int)i.StatusVal == 0 && i.InspectionDate != default(DateTime) && i.InspectionDate.Date < kpiToday);
                var cntNew       = allRaw.Count(i => (int)i.StatusVal == 0) - cntDelay;
                var cntPass      = allRaw.Count(i => string.Equals(i.FinalResult, "PASSED", StringComparison.OrdinalIgnoreCase));
                var cntFail      = allRaw.Count(i => string.Equals(i.FinalResult, "FAILED", StringComparison.OrdinalIgnoreCase));

                // ── Filter in-memory ──
                // DB mapping: 0=New, 1=OnGoing, 2=Completed, 3=Cancel, 4=Pending
                // UI hiển thị: New | Delay | Completed | Cancel
                // → "Delay" = Status=0 (New) VÀ InspectionDate < hôm nay (quá hạn mà chưa làm)
                // → "New"   = Status=0 (New) VÀ InspectionDate >= hôm nay (chưa tới hạn)
                var today = DateTime.Today;
                if (!string.IsNullOrWhiteSpace(status))
                {
                    var key = status.Trim().ToLower().Replace(" ", "").Replace("_", "");
                    if (key == "delay")
                    {
                        // Delay = Status=0 (New) AND InspectionDate < today
                        allRaw = allRaw.Where(i =>
                            (int)i.StatusVal == 0 &&
                            i.InspectionDate != default(DateTime) &&
                            i.InspectionDate.Date < today
                        ).ToList();
                    }
                    else if (key == "new")
                    {
                        // New = Status=0 (New) AND (InspectionDate >= today HOẶC chưa set ngày)
                        allRaw = allRaw.Where(i =>
                            (int)i.StatusVal == 0 &&
                            (i.InspectionDate == default(DateTime) || i.InspectionDate.Date >= today)
                        ).ToList();
                    }
                    else
                    {
                        var sv = key switch
                        {
                            "ongoing"   => (int?)1,
                            "completed" => (int?)2,
                            "cancel"    => (int?)3,
                            "cancelled" => (int?)3,
                            "pending"   => (int?)4,
                            "draft"     => (int?)9,
                            _           => null
                        };
                        if (sv.HasValue)
                            allRaw = allRaw.Where(i => (int)i.StatusVal == sv.Value).ToList();
                    }
                }

                if (!string.IsNullOrWhiteSpace(customerId))
                    allRaw = allRaw.Where(i => string.Equals(i.CustomerId, customerId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (!string.IsNullOrWhiteSpace(vendorId))
                    allRaw = allRaw.Where(i => string.Equals(i.VendorId, vendorId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (!string.IsNullOrWhiteSpace(inspectionType))
                    allRaw = allRaw.Where(i => string.Equals(i.InspectionTypeVal.ToString(), inspectionType, StringComparison.OrdinalIgnoreCase)).ToList();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.ToLower();
                    allRaw = allRaw.Where(i =>
                        (i.JobNumber != null && i.JobNumber.ToLower().Contains(s)) ||
                        (i.CustomerName != null && i.CustomerName.ToLower().Contains(s)) ||
                        (i.VendorName != null && i.VendorName.ToLower().Contains(s)) ||
                        (i.ProductName != null && i.ProductName.ToLower().Contains(s)) ||
                        (i.Title != null && i.Title.ToLower().Contains(s))
                    ).ToList();
                }

                var total = allRaw.Count;
                var pageItems = allRaw
                    .OrderByDescending(i => i.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                if (pageItems.Count == 0)
                    return Ok(new { total, page, pageSize, items = new List<object>(), cntAll, cntCompleted, cntCancel, cntDelay, cntNew, cntPass, cntFail });

                // ── Lookups: load toàn bộ vào memory, tránh Contains() gây lỗi SQL Server cũ ──

                // Inspector lookup — load tất cả users có InspectorId
                var allInspectors = await _userManager.Users
                    .Where(u => u.InspectorId != null && u.InspectorId != "")
                    .Select(u => new { u.InspectorId, u.FullName, u.Mobile })
                    .ToListAsync();
                var inspectorByCode       = new Dictionary<string, string>();
                var inspectorMobileByCode = new Dictionary<string, string>();
                foreach (var u in allInspectors)
                {
                    if (u.InspectorId != null && !inspectorByCode.ContainsKey(u.InspectorId))
                    {
                        inspectorByCode[u.InspectorId]       = u.FullName ?? "";
                        inspectorMobileByCode[u.InspectorId] = u.Mobile ?? "";
                    }
                }

                // Vendor lookup — load tất cả vendors (bao gồm contact info)
                var allVendors = await _db.Vendors
                    .Select(v => new
                    {
                        v.Code,
                        v.Name,
                        v.CompanyAddress,
                        v.Country,
                        v.Address1,
                        v.Address2,
                        v.City,
                        v.State,
                        v.Phone,
                        v.ContactName,
                        v.ContactTitle,
                        v.ContactPhone,
                        v.ContactEmail
                    })
                    .ToListAsync();

                // Index by Code (primary) and by Name (fallback)
                var vendorByCode = allVendors.GroupBy(v => v.Code).ToDictionary(g => g.Key, g => g.First());
                var vendorByName = allVendors.GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.First());

                var vendorAddressDict = allVendors.ToDictionary(v => v.Code,
                    v => !string.IsNullOrEmpty(v.Address1) ? v.Address1 : v.CompanyAddress ?? "");
                var vendorCountryDict = allVendors.ToDictionary(v => v.Code, v => v.Country ?? "");
                var vendorContactNameDict = allVendors.ToDictionary(v => v.Code, v => v.ContactName ?? "");
                var vendorContactTitleDict = allVendors.ToDictionary(v => v.Code, v => v.ContactTitle ?? "");
                var vendorContactPhoneDict = allVendors.ToDictionary(v => v.Code, v => v.ContactPhone ?? "");
                var vendorContactOfficeDict = allVendors.ToDictionary(v => v.Code, v => v.Phone ?? "");
                var vendorContactEmailDict = allVendors.ToDictionary(v => v.Code, v => v.ContactEmail ?? "");

                // Product type lookup — load tất cả products
                var allProducts = await _db.Products
                    .Select(p => new { p.ProductName, p.ProductType })
                    .ToListAsync();
                var productTypeDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in allProducts)
                    if (p.ProductName != null && !productTypeDict.ContainsKey(p.ProductName))
                        productTypeDict[p.ProductName] = p.ProductType ?? "";

                // References lookup — load qua ProductReferences (link theo ItemNumber → Product.Id)
                // KHÔNG dùng InspectionReferences nữa: references thuộc về SẢN PHẨM, không thuộc inspection.
                // 1. Lấy danh sách ItemNumber unique của các inspection trên trang
                var pageItemNumbers = pageItems
                    .Where(i => !string.IsNullOrEmpty(i.ItemNumber))
                    .Select(i => i.ItemNumber!)
                    .Distinct()
                    .ToList();

                // 2. Map ItemNumber → ProductId
                var productIdByItemNumber = pageItemNumbers.Count > 0
                    ? await _db.Products
                        .AsNoTracking()
                        .Where(p => p.ItemNumber != null && pageItemNumbers.Contains(p.ItemNumber))
                        .Select(p => new { p.Id, p.ItemNumber })
                        .ToDictionaryAsync(p => p.ItemNumber!, p => p.Id, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                // 3. Load ProductReferences cho các ProductId đã tìm được
                var productIds = productIdByItemNumber.Values.Distinct().ToList();
                var productRefs = productIds.Count > 0
                    ? await _db.ProductReferences
                        .AsNoTracking()
                        .Where(r => productIds.Contains(r.ProductId))
                        .OrderBy(r => r.ProductId).ThenBy(r => r.SortOrder)
                        .Select(r => new
                        {
                            r.ProductId,
                            ReferenceName = r.Name,
                            r.FileName,
                            r.FileUrl
                        })
                        .ToListAsync()
                    : new();

                // 4. Group references theo ProductId → list
                var refsByProductId = productRefs
                    .GroupBy(r => r.ProductId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // ── Map enums → strings in-memory ──
                // DB enum: 0=New, 1=OnGoing, 2=Completed, 3=Cancel, 4=Pending
                // UI hiển thị: New | Delay | Completed | Cancel
                // → Status=0 (New) + InspectionDate < hôm nay  ⇒ "Delay"
                // → Status=0 (New) + InspectionDate >= hôm nay ⇒ "New"
                // → 1 (OnGoing) và 4 (Pending) legacy: vẫn map sang "Delay" để tương thích dữ liệu cũ
                string MapStatus(InspectionStatus st, DateTime inspDate) => (int)st switch
                {
                    0 => (inspDate != default(DateTime) && inspDate.Date < today) ? "Delay" : "New",
                    1 => "Delay",      // legacy OnGoing
                    2 => "Completed",
                    3 => "Cancel",
                    4 => "Delay",      // legacy Pending
                    9 => "Draft",
                    _ => "New"
                };
                static string MapInspType(InspectionType t) => t switch
                {
                    InspectionType.DPI => "DPI",
                    InspectionType.PPT => "PPT",
                    InspectionType.PST => "PST",
                    _ => "DPI"
                };

                var items = pageItems.Select(i =>
                {
                    var iid = i.InspectorId ?? "";
                    var inspName   = inspectorByCode.TryGetValue(iid, out var n)   ? n   : i.InspectorName ?? "";
                    var inspMobile = inspectorMobileByCode.TryGetValue(iid, out var m) ? m : "";

                    // Vendor lookup: try by Code first, then by VendorName stored in inspection
                    var vid = i.VendorId ?? "";
                    var vByCode = vendorByCode.TryGetValue(vid, out var vbc) ? vbc : null;
                    var vByName = vByCode == null && vendorByName.TryGetValue(i.VendorName ?? "", out var vbn) ? vbn : null;
                    var v = vByCode ?? vByName;

                    var vAddr = v != null ? (!string.IsNullOrEmpty(v.Address1) ? v.Address1 : v.CompanyAddress ?? "") : "";
                    var vAddr1 = v?.Address1 ?? "";
                    var vAddr2 = v?.Address2 ?? "";
                    var vCity = v?.City ?? "";
                    var vState = v?.State ?? "";
                    var vCountry = v?.Country ?? "";
                    var vcName = v?.ContactName ?? "";
                    var vcTitle = v?.ContactTitle ?? "";
                    var vcPhone = v?.ContactPhone ?? "";
                    var vcOffice = v?.Phone ?? "";
                    var vcEmail = v?.ContactEmail ?? "";

                    productTypeDict.TryGetValue(i.ProductName ?? "", out var ptype);

                    // References của SẢN PHẨM (link qua ItemNumber → Product.Id → ProductReferences)
                    var refList = new List<object>();
                    if (!string.IsNullOrEmpty(i.ItemNumber)
                        && productIdByItemNumber.TryGetValue(i.ItemNumber, out var prodId)
                        && refsByProductId.TryGetValue(prodId, out var prs))
                    {
                        refList = prs.Select(r => (object)new
                        {
                            referenceName = r.ReferenceName,
                            fileName      = r.FileName,
                            fileUrl       = r.FileUrl,
                            remark        = (string?)null   // ProductReferences không có Remark; giữ field cho FE compat
                        }).ToList();
                    }

                    return (object)new
                    {
                        id = i.Id,
                        title = i.Title,
                        status = MapStatus(i.StatusVal, i.InspectionDate),
                        createdAt = i.CreatedAt,
                        completedAt = i.CompletedAt,
                        jobNumber = i.JobNumber,
                        inspectionDate = i.InspectionDate,
                        customerName = i.CustomerName,
                        customerId = i.CustomerId,
                        vendorName = i.VendorName,
                        vendorId = i.VendorId,
                        vendorAddress = vAddr,
                        vendorAddress1 = vAddr1,
                        vendorAddress2 = vAddr2,
                        vendorCity = vCity,
                        vendorState = vState,
                        vendorCountry = vCountry,
                        vendorContactName = vcName,
                        vendorContactPosition = vcTitle,
                        vendorContactMobile = vcPhone,
                        vendorContactOffice = vcOffice,
                        vendorContactEmail = vcEmail,
                        productCategory = i.ProductCategory,
                        productType = ptype ?? "",
                        productName = i.ProductName ?? "",
                        itemNumber = i.ItemNumber ?? "",
                        inspectionType = MapInspType(i.InspectionTypeVal),
                        inspectorId = iid,
                        inspectorName = inspName,
                        inspectorMobile = inspMobile,
                        poNumber = i.PoNumber ?? "",
                        finalResult = i.FinalResult,
                        references = refList
                    };
                }).ToList();

                return Ok(new { total, page, pageSize, items, cntAll, cntCompleted, cntCancel, cntDelay, cntNew, cntPass, cntFail });
            }
            catch (Exception ex)
            {
                // Trả về lỗi chi tiết để debug
                var msg = ex.Message;
                var inner = ex.InnerException?.Message ?? "";
                var stack = ex.StackTrace ?? "";
                return StatusCode(500, new { error = msg, inner, stack });
            }
        }
        // GET api/inspections/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
          try
          {
            var inspection = await _db.Inspections
                .IgnoreQueryFilters()
                .Include(i => i.CreatedBy)
                .Include(i => i.OverallConclusions.OrderBy(o => o.Order))
                .Include(i => i.Packaging)
                .Include(i => i.ProductSpec)
                .Include(i => i.ColourSwatches.OrderBy(c => c.Order))
                .Include(i => i.PerformanceTests.OrderBy(p => p.Order))
                .Include(i => i.References.OrderBy(r => r.Order))
                .Include(i => i.Cartons.OrderBy(c => c.CartonIndex))
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inspection == null) return NotFound();
            var custScope = await HttpContext.RequestServices.GetRequiredService<UserAccess>().CustomerScopeAsync();
            if (custScope != null && !custScope.Contains(inspection.CustomerId ?? "")) return NotFound();

            // Lookup ProductCode — gộp vào 1 query, không cần round-trip thứ 2
            var productCode = string.IsNullOrEmpty(inspection.ProductName) ? "" :
                (await _db.Products.AsNoTracking()
                    .Where(p => p.ProductName == inspection.ProductName)
                    .Select(p => new { p.ProductCode })
                    .FirstOrDefaultAsync())?.ProductCode ?? "";

            // Load ProductReferences theo Inspection.ItemNumber → Product.Id
            // (References thuộc về SẢN PHẨM, không thuộc inspection)
            var productReferencesList = new List<object>();
            if (!string.IsNullOrEmpty(inspection.ItemNumber))
            {
                var prodId = await _db.Products.AsNoTracking()
                    .Where(p => p.ItemNumber == inspection.ItemNumber)
                    .Select(p => (int?)p.Id)
                    .FirstOrDefaultAsync();
                if (prodId.HasValue)
                {
                    productReferencesList = await _db.ProductReferences.AsNoTracking()
                        .Where(r => r.ProductId == prodId.Value)
                        .OrderBy(r => r.SortOrder)
                        .Select(r => (object)new
                        {
                            referenceName = r.Name,
                            fileName      = r.FileName,
                            fileUrl       = r.FileUrl,
                            remark        = (string?)null
                        })
                        .ToListAsync();
                }
            }

            // Lookup Country của Inspector từ AspNetUsers (dùng làm Inspection Location)
            var inspectorCountry = "";
            if (!string.IsNullOrEmpty(inspection.InspectorId))
            {
                inspectorCountry = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => u.InspectorId == inspection.InspectorId)
                    .Select(u => u.Country)
                    .FirstOrDefaultAsync() ?? "";
            }

            // Lookup Vendor — địa chỉ và thông tin liên hệ (Vendor.cs: ContactName, ContactTitle, ContactPhone, ContactEmail)
            string vendorAddress = "", vendorAddress1 = "", vendorAddress2 = "", vendorCity = "", vendorState = "", vendorCountry = "";
            string contactName = "", contactTitle = "", contactPhone = "", contactOffice = "", contactEmail = "";
            if (!string.IsNullOrEmpty(inspection.VendorId) || !string.IsNullOrEmpty(inspection.VendorName))
            {
                var vendor = await _db.Vendors
                    .AsNoTracking()
                    .Where(v => v.Code == inspection.VendorId || v.Name == inspection.VendorName)
                    .FirstOrDefaultAsync();
                if (vendor != null)
                {
                    vendorAddress = !string.IsNullOrEmpty(vendor.Address1)
                                    ? vendor.Address1
                                    : vendor.CompanyAddress ?? "";
                    vendorAddress1 = vendor.Address1 ?? "";
                    vendorAddress2 = vendor.Address2 ?? "";
                    vendorCity = vendor.City ?? "";
                    vendorState = vendor.State ?? "";
                    vendorCountry = vendor.Country ?? "";
                    contactName = vendor.ContactName ?? "";
                    contactTitle = vendor.ContactTitle ?? "";
                    contactPhone = vendor.ContactPhone ?? "";
                    contactOffice = vendor.Phone ?? "";
                    contactEmail = vendor.ContactEmail ?? "";
                }
            }

            // MapStatus: nhất quán với GetAll
            // DB: 0=New, 1=OnGoing, 2=Completed, 3=Cancel, 4=Pending
            // UI: New | Delay | Completed | Cancel
            //   → Status=0 + InspectionDate < today  ⇒ Delay
            //   → Status=0 + InspectionDate >= today ⇒ New
            //   → 1 (OnGoing) và 4 (Pending) legacy: vẫn map sang Delay
            var todayForEdit = DateTime.Today;
            string MapStatusForEdit(InspectionStatus st, DateTime inspDate) => (int)st switch
            {
                0 => (inspDate != default(DateTime) && inspDate.Date < todayForEdit) ? "Delay" : "New",
                1 => "Delay",      // legacy OnGoing
                2 => "Completed",
                3 => "Cancel",
                4 => "Delay",      // legacy Pending
                9 => "Draft",
                _ => "New"
            };

            // Map AQL enums → string
            static string MapDefectAql(DefectAqlLevel? v) => v switch
            {
                DefectAqlLevel.AQL_0_065 => "0.065",
                DefectAqlLevel.AQL_0_1 => "0.1",
                DefectAqlLevel.AQL_0_15 => "0.15",
                DefectAqlLevel.AQL_0_25 => "0.25",
                DefectAqlLevel.AQL_0_4 => "0.40",
                DefectAqlLevel.AQL_0_65 => "0.65",
                DefectAqlLevel.AQL_1_0 => "1.0",
                DefectAqlLevel.AQL_1_5 => "1.5",
                DefectAqlLevel.AQL_2_5 => "2.5",
                DefectAqlLevel.AQL_4_0 => "4.0",
                DefectAqlLevel.AQL_6_5 => "6.5",
                _ => "Not Allowed"
            };
            static string MapAqlLevel(AqlInspectionLevel v) => v switch
            {
                AqlInspectionLevel.I => "I",
                AqlInspectionLevel.III => "III",
                AqlInspectionLevel.S1 => "S1",
                AqlInspectionLevel.S2 => "S2",
                AqlInspectionLevel.S3 => "S3",
                AqlInspectionLevel.S4 => "S4",
                _ => "II"
            };
            static string MapPkgType(PackagingType? v) => v switch
            {
                PackagingType.FSCCarton => "FSC Carton",
                PackagingType.NonFSCCarton => "Non-FSC Carton",
                _ => ""
            };
            static string MapCartonColor(CartonColor? v) => v switch
            {
                CartonColor.White => "White",
                CartonColor.Brown => "Brown",
                _ => ""
            };
            static string MapCardboard(CardboardType? v) => v switch
            {
                CardboardType.SingleFace => "Single face",
                CardboardType.SingleWall => "Single Wall",
                CardboardType.DoubleWall => "Double Wall",
                _ => ""
            };
            static string MapShipping(ShippingMarkType? v) => v switch
            {
                ShippingMarkType.ColorLabel => "Color Label",
                ShippingMarkType.Printing => "Printing",
                _ => ""
            };

            // Toàn bộ ảnh sản phẩm trong hồ sơ (ảnh chính đứng đầu) để màn QC hiển thị thư viện xem/phóng to
            var prodPhotoUrls = new List<string>();
            {
                var prod = await _db.Products.AsNoTracking()
                    .Where(p => (!string.IsNullOrEmpty(inspection.ItemNumber) && p.ItemNumber == inspection.ItemNumber)
                             || p.ProductName == inspection.ProductName)
                    .OrderByDescending(p => p.ItemNumber == inspection.ItemNumber)
                    .FirstOrDefaultAsync();
                if (prod != null)
                {
                    prodPhotoUrls = ProductController.ParsePhotoUrls(prod.PhotoUrlsJson, prod.PhotoUrl);
                    if (!string.IsNullOrEmpty(prod.PhotoUrl) && prodPhotoUrls.Remove(prod.PhotoUrl))
                        prodPhotoUrls.Insert(0, prod.PhotoUrl);
                }
            }

            return Ok(new
            {
                id = inspection.Id,
                title = inspection.Title,
                description = inspection.Description,
                status = MapStatusForEdit(inspection.Status, inspection.InspectionDate),
                jobNumber = inspection.JobNumber,
                customerName = inspection.CustomerName,
                customerId = inspection.CustomerId,
                vendorName = inspection.VendorName,
                vendorId = inspection.VendorId,
                vendorAddress = vendorAddress,
                vendorAddress1 = vendorAddress1,
                vendorAddress2 = vendorAddress2,
                vendorCity = vendorCity,
                vendorState = vendorState,
                vendorCountry = vendorCountry,
                contactName = contactName,
                contactTitle = contactTitle,
                contactPhone = contactPhone,
                contactOffice = contactOffice,
                contactEmail = contactEmail,
                inspectionLocation = !string.IsNullOrEmpty(inspectorCountry)
                    ? inspectorCountry
                    : inspection.InspectionLocation,
                poNumber = inspection.PoNumber,
                inspectionDate = inspection.InspectionDate,
                itemNumber = inspection.ItemNumber,
                inspectionType = inspection.InspectionType.ToString(),
                productName = inspection.ProductName,
                productCode = productCode,
                productCategory = inspection.ProductCategory,
                totalShipmentQty = inspection.TotalShipmentQty,
                totalCartonBoxes = inspection.TotalCartonBoxes,
                generalRemark = inspection.GeneralRemark,
                inspectorId = inspection.InspectorId,
                inspectorName = inspection.InspectorName,
                inspectionStartTime = inspection.InspectionStartTime,
                inspectionEndTime = inspection.InspectionEndTime,
                inspectorRemarks = inspection.InspectorRemarks,
                createdAt = inspection.CreatedAt,
                completedAt = inspection.CompletedAt,
                createdById = inspection.CreatedById,
                createdByName = inspection.CreatedBy?.FullName ?? "",

                // AQL
                aqlQuantity = inspection.TotalShipmentQty, // dùng TotalShipmentQty nếu AqlQuantity chưa có field riêng
                aqlLevel = MapAqlLevel(inspection.AqlInspectionLevel),
                aqlCritical = MapDefectAql(inspection.CriticalAql),
                aqlMajor = MapDefectAql(inspection.MajorAql),
                aqlMinor = MapDefectAql(inspection.MinorAql),
                aqlCriticalSampleSize = inspection.CriticalSampleSize,
                aqlCriticalAccept = inspection.CriticalAccept,
                aqlCriticalReject = inspection.CriticalReject,
                aqlMajorSampleSize = inspection.MajorSampleSize,
                aqlMajorAccept = inspection.MajorAccept,
                aqlMajorReject = inspection.MajorReject,
                aqlMinorSampleSize = inspection.MinorSampleSize,
                aqlMinorAccept = inspection.MinorAccept,
                aqlMinorReject = inspection.MinorReject,
                photo1Url = inspection.Photo1Url,
                photo2Url = inspection.Photo2Url,
                productPhotoUrls = prodPhotoUrls,
                finalResult = inspection.FinalResult,
                inspectorComments = inspection.InspectorComments,
                signatureUrl = inspection.SignatureUrl,
                inspectionReference = inspection.QcInspectionRef,

                // Packaging
                packaging = inspection.Packaging == null ? null : new
                {
                    itemNumber = inspection.Packaging.ItemNumber,
                    cartonNumber = inspection.Packaging.CartonNumber,
                    packagingType = MapPkgType(inspection.Packaging.PackagingType),
                    cartonColor = MapCartonColor(inspection.Packaging.CartonColor),
                    cardboardType = MapCardboard(inspection.Packaging.CardboardType),
                    shippingMark = MapShipping(inspection.Packaging.ShippingMark),
                    hasBarcode = inspection.Packaging.HasBarcode,
                    innerPackingQty = inspection.Packaging.InnerPackingQty,
                    innerL = inspection.Packaging.InnerSizeL,
                    innerW = inspection.Packaging.InnerSizeW,
                    innerH = inspection.Packaging.InnerSizeH,
                    innerWeight = inspection.Packaging.InnerWeight,
                    innerPackingRemark = inspection.Packaging.InnerPackingRemark,
                    outerL = inspection.Packaging.OuterSizeL,
                    outerW = inspection.Packaging.OuterSizeW,
                    outerH = inspection.Packaging.OuterSizeH,
                    outerWeight = inspection.Packaging.OuterWeight,
                    assemblyInstruction = inspection.Packaging.AssemblyInstruction,
                    hardware = inspection.Packaging.Hardware,
                    burstingTest = inspection.Packaging.BurstingTest,
                    cartons = inspection.Cartons.OrderBy(c => c.CartonIndex).Select(c => new
                    {
                        cartonIndex = c.CartonIndex,
                        cartonVariant = c.CartonVariant,
                        description = c.Description,
                        packingQuantity = c.PackingQuantity,
                        innerPackingQty = c.InnerPackingQty,
                        innerL = c.InnerSizeL, innerW = c.InnerSizeW, innerH = c.InnerSizeH, innerWeight = c.InnerWeight,
                        outerL = c.OuterSizeL, outerW = c.OuterSizeW, outerH = c.OuterSizeH, outerWeight = c.OuterWeight
                    }).ToList()
                },

                // Product Spec
                productSpec = inspection.ProductSpec == null ? null : new
                {
                    sizeL = inspection.ProductSpec.SizeL,
                    sizeW = inspection.ProductSpec.SizeW,
                    sizeH = inspection.ProductSpec.SizeH,
                    weight = inspection.ProductSpec.Weight,
                    actualSizeL = inspection.ProductSpec.ActualSizeL,
                    actualSizeW = inspection.ProductSpec.ActualSizeW,
                    actualSizeH = inspection.ProductSpec.ActualSizeH,
                    compareGoldenSample = inspection.ProductSpec.CompareGoldenSample
                },

                // Colour Swatches
                colourSwatches = inspection.ColourSwatches.Select(c => new { material = c.Material, remark = c.Remark }).ToList(),

                // Performance Tests
                performanceTests = inspection.PerformanceTests.Select(t => new
                {
                    masterId = t.MasterId,
                    category = t.Category,
                    testItem = t.TestItem,
                    testProtocol = t.TestProtocol,
                    testRequirement = t.TestRequirement,
                    remark = t.Remark
                }),

                // References — lấy từ ProductReferences (link qua ItemNumber → Product.Id)
                references = productReferencesList,

                // Steps & Overall Conclusions
                steps = inspection.Steps.Select(s => new
                {
                    id = s.Id,
                    order = s.Order,
                    title = s.Title,
                    description = s.Description,
                    status = s.Status.ToString(),
                    note = s.Note,
                    completedAt = s.CompletedAt
                }),
                overallConclusions = inspection.OverallConclusions.Select(o => new
                {
                    id = o.Id,
                    order = o.Order,
                    letter = o.Letter,
                    label = o.Label,
                    compliance = o.Compliance.ToString(),
                    remark = o.Remark
                }),

                // QC Result — raw JSON string, client tự parse
                qcResultJson = inspection.QcResultJson ?? ""
            });
          }
          catch (Exception ex)
          {
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
          }
        }

        // POST api/inspections
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] CreateInspectionRequest request)
        {
          if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.JobRequest, PermAction.Create)) is { } denied) return denied;
          try
          {
            // Thử nhiều claim type — JWT có thể dùng "sub" hoặc NameIdentifier
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;

            // Nếu vẫn null → lấy user đầu tiên trong DB (debug mode)
            // TODO: xóa fallback này sau khi fix CORS/JWT trên server
            if (string.IsNullOrEmpty(userId))
            {
                var first = await _userManager.Users
                    .OrderBy(u => u.CreatedAt)
                    .FirstOrDefaultAsync();
                if (first == null) return Unauthorized();
                userId = first.Id;
            }

            // ── Sinh JobNumber tự động theo InspectionType ──
            // Format: {TYPE}-JN{100001+n}-{YY}
            // Ví dụ: DPI-JN100001-26, PPT-JN100003-26
            var typePrefix = (request.InspectionType?.ToUpper()) switch
            {
                "PPT" => "PPT",
                "PST" => "PST",
                _ => "DPI"
            };
            var yearSuffix = (DateTime.UtcNow.Year % 100).ToString("D2"); // "26", "27", ...
            var jnPrefix = $"{typePrefix}-JN";

            // Đếm số inspection cùng loại để lấy số thứ tự tiếp theo
            var count = await _db.Inspections.IgnoreQueryFilters()
                .CountAsync(i => i.JobNumber != null && i.JobNumber.StartsWith(jnPrefix));
            var jobNumber = $"{typePrefix}-JN{100000 + count + 1}-{yearSuffix}";

            var inspType = Enum.TryParse<InspectionType>(request.InspectionType, true, out var parsedType)
                ? parsedType
                : InticooInspection.Domain.Entities.InspectionType.DPI;

            // ── Parse AQL enums ──
            var aqlLevel = Enum.TryParse<AqlInspectionLevel>(request.AqlLevel, true, out var parsedAqlLevel)
                ? parsedAqlLevel : AqlInspectionLevel.II;

            static DefectAqlLevel ParseDefectAql(string? val) => val switch
            {
                "0.065" => DefectAqlLevel.AQL_0_065,
                "0.10" or "0.1" => DefectAqlLevel.AQL_0_1,
                "0.15" => DefectAqlLevel.AQL_0_15,
                "0.25" => DefectAqlLevel.AQL_0_25,
                "0.40" or "0.4" => DefectAqlLevel.AQL_0_4,
                "0.65" => DefectAqlLevel.AQL_0_65,
                "1.0" => DefectAqlLevel.AQL_1_0,
                "1.5" => DefectAqlLevel.AQL_1_5,
                "2.5" => DefectAqlLevel.AQL_2_5,
                "4.0" => DefectAqlLevel.AQL_4_0,
                "6.5" => DefectAqlLevel.AQL_6_5,
                _ => DefectAqlLevel.NotAllowed
            };

            // ── Parse Packaging enum helpers ──
            static PackagingType? ParsePkgType(string? v) => v switch
            {
                "FSC Carton" => PackagingType.FSCCarton,
                "Non-FSC Carton" => PackagingType.NonFSCCarton,
                _ => null
            };
            static CartonColor? ParseCartonColor(string? v) => v switch
            {
                "White" => CartonColor.White,
                "Brown" => CartonColor.Brown,
                _ => null
            };
            static CardboardType? ParseCardboard(string? v) => v switch
            {
                "Single face" => CardboardType.SingleFace,
                "Single Wall" => CardboardType.SingleWall,
                "Double Wall" => CardboardType.DoubleWall,
                _ => null
            };
            static ShippingMarkType? ParseShipping(string? v) => v switch
            {
                "Color Label" => ShippingMarkType.ColorLabel,
                "Printing" => ShippingMarkType.Printing,
                _ => null
            };

            var inspection = new Inspection
            {
                Title = request.Title,
                Description = request.Description,
                CustomerName = request.CustomerName,
                CustomerId = request.CustomerId,
                VendorName = request.VendorName,
                VendorId = request.VendorId,
                InspectionLocation = request.InspectionLocation,
                PoNumber = request.PoNumber,
                InspectionDate = request.InspectionDate ?? DateTime.UtcNow,
                ItemNumber = request.ItemNumber,
                InspectionType = inspType,
                ProductName = request.ProductName,
                ProductCategory = request.ProductCategory,
                TotalShipmentQty = request.TotalShipmentQty,
                TotalCartonBoxes = request.TotalCartonBoxes,
                GeneralRemark = request.GeneralRemark,
                InspectorId = request.InspectorId,
                InspectorName = request.InspectorName,
                InspectionStartTime = request.InspectionStartTime,
                InspectionEndTime = request.InspectionEndTime,
                InspectorRemarks = request.InspectorRemarks is { Length: > 500 } r ? r[..500] : request.InspectorRemarks,
                JobNumber = jobNumber,

                // AQL
                AqlInspectionLevel = aqlLevel,
                CriticalAql = ParseDefectAql(request.AqlCritical),
                MajorAql = ParseDefectAql(request.AqlMajor),
                MinorAql = ParseDefectAql(request.AqlMinor),
                CriticalSampleSize = request.AqlCriticalSampleSize,
                CriticalAccept = request.AqlCriticalAccept,
                CriticalReject = request.AqlCriticalReject,
                MajorSampleSize = request.AqlMajorSampleSize,
                MajorAccept = request.AqlMajorAccept,
                MajorReject = request.AqlMajorReject,
                MinorSampleSize = request.AqlMinorSampleSize,
                MinorAccept = request.AqlMinorAccept,
                MinorReject = request.AqlMinorReject,
                Photo1Url = request.Photo1Url,
                Photo2Url = request.Photo2Url,

                // B. Packaging & Identification
                Packaging = request.Packaging == null ? null : new InspectionPackaging
                {
                    ItemNumber = request.Packaging.ItemNumber,
                    CartonNumber = request.Packaging.CartonNumber,
                    PackagingType = ParsePkgType(request.Packaging.PackagingType),
                    CartonColor = ParseCartonColor(request.Packaging.CartonColor),
                    CardboardType = ParseCardboard(request.Packaging.CardboardType),
                    ShippingMark = ParseShipping(request.Packaging.ShippingMark),
                    HasBarcode = request.Packaging.HasBarcode,
                    InnerPackingQty = request.Packaging.InnerPackingQty,
                    InnerSizeL = request.Packaging.InnerL,
                    InnerSizeW = request.Packaging.InnerW,
                    InnerSizeH = request.Packaging.InnerH,
                    InnerWeight = request.Packaging.InnerWeight,
                    InnerPackingRemark = request.Packaging.InnerPackingRemark,
                    OuterSizeL = request.Packaging.OuterL,
                    OuterSizeW = request.Packaging.OuterW,
                    OuterSizeH = request.Packaging.OuterH,
                    OuterWeight = request.Packaging.OuterWeight,
                    AssemblyInstruction = request.Packaging.AssemblyInstruction,
                    Hardware = request.Packaging.Hardware,
                    BurstingTest = request.Packaging.BurstingTest
                },
                Cartons = BuildCartons(request.Packaging),

                // C. Product Specification
                ProductSpec = request.ProductSpec == null ? null : new InspectionProductSpec
                {
                    SizeL = request.ProductSpec.SizeL,
                    SizeW = request.ProductSpec.SizeW,
                    SizeH = request.ProductSpec.SizeH,
                    Weight = request.ProductSpec.Weight,
                    CompareGoldenSample = request.ProductSpec.CompareGoldenSample
                },

                // C-iii. Colour Swatches
                ColourSwatches = (request.ProductSpec?.ColourSwatches ?? Array.Empty<ColourSwatchRequest>())
                    .Select((c, i) => new InspectionColourSwatch
                    {
                        Order    = i + 1,
                        Material = c.Material ?? "",
                        Remark   = c.Remark   ?? ""
                    })
                    .ToList(),

                // D. Performance Testing
                PerformanceTests = (request.PerformanceTests ?? new())
                    .Select((t, i) => new InspectionPerformanceTest
                    {
                        Order = i + 1,
                        MasterId = t.MasterId,
                        Category = t.Category,
                        TestItem = t.TestItem,
                        TestProtocol = t.TestProtocol,
                        TestRequirement = t.TestRequirement,
                        Remark = t.Remark
                    }).ToList(),

                // E. References
                References = (request.References ?? new())
                    .Select((r, i) => new InspectionReference
                    {
                        Order = i + 1,
                        ReferenceName = r.ReferenceName,
                        FileName = r.FileName,
                        FileUrl = r.FileUrl,
                        Remark = r.Remark
                    }).ToList(),

                Status = request.IsDraft == true ? InspectionStatus.Draft : InspectionStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId,
                Steps = request.Steps.Select((s, i) => new InspectionStep
                {
                    Order = i + 1,
                    Title = s.Title,
                    Description = s.Description,
                    Status = StepStatus.Pending
                }).ToList(),
                OverallConclusions = request.OverallConclusions
                    .Select((o, i) => new InspectionOverallConclusion
                    {
                        Order = i + 1,
                        Letter = o.Letter,
                        Label = o.Label,
                        Compliance = Enum.TryParse<OverallCompliance>(o.Compliance, true, out var cp) ? cp : OverallCompliance.None,
                        Remark = o.Remark
                    }).ToList()
            };

            _db.Inspections.Add(inspection);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = inspection.Id });
          }
          catch (Exception ex)
          {
            // Phơi lỗi chi tiết để chẩn đoán (giống pattern ở Update/GetAll)
            return StatusCode(500, new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message,
                stack = ex.StackTrace
            });
          }
        }

        // PUT api/inspections/{id}/status
        [HttpPut("{id}/status")]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.JobRequest, PermAction.Edit), (ModuleKeys.InspectionSummary, PermAction.Edit)) is { } denied) return denied;
            var inspection = await _db.Inspections.FindAsync(id);
            if (inspection == null) return NotFound();

            if (Enum.TryParse<InspectionStatus>(request.Status, true, out var status))
            {
                inspection.Status = status;
                if (status == InspectionStatus.Completed)
                    inspection.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        // PUT api/inspections/{id}/steps/{stepId}
        [HttpPut("{id}/steps/{stepId}")]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateStep(int id, int stepId, [FromBody] UpdateStepRequest request)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.InspectionSchedule, PermAction.Use), (ModuleKeys.InspectionSummary, PermAction.Create), (ModuleKeys.InspectionSummary, PermAction.Edit), (ModuleKeys.JobRequest, PermAction.Edit)) is { } denied) return denied;
            var step = await _db.InspectionSteps.FindAsync(stepId);
            if (step == null || step.InspectionId != id) return NotFound();

            if (Enum.TryParse<StepStatus>(request.Status, true, out var status))
            {
                step.Status = status;
                step.Note = request.Note;
                if (status != StepStatus.Pending)
                    step.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        // DELETE api/inspections/{id}
        [HttpDelete("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Delete(int id)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.JobRequest, PermAction.Delete), (ModuleKeys.InspectionSummary, PermAction.Delete)) is { } denied) return denied;
            var inspection = await _db.Inspections
                .IgnoreQueryFilters()
                .Include(i => i.Steps)
                .Include(i => i.OverallConclusions)
                .Include(i => i.Packaging)
                .Include(i => i.ProductSpec)
                .Include(i => i.ColourSwatches)
                .Include(i => i.PerformanceTests)
                .Include(i => i.References)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (inspection == null) return NotFound();
            _db.Inspections.Remove(inspection);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // PUT api/inspections/{id}/overall
        [HttpPut("{id}/overall")]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateOverall(int id, [FromBody] List<OverallConclusionRequest> request)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.InspectionSchedule, PermAction.Use), (ModuleKeys.InspectionSummary, PermAction.Create), (ModuleKeys.InspectionSummary, PermAction.Edit)) is { } denied) return denied;
            var inspection = await _db.Inspections
                .Include(i => i.OverallConclusions)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (inspection == null) return NotFound();

            // Xóa cũ, thêm mới
            _db.RemoveRange(inspection.OverallConclusions);
            inspection.OverallConclusions = request
                .Select((o, i) => new InspectionOverallConclusion
                {
                    InspectionId = id,
                    Order = i + 1,
                    Letter = o.Letter,
                    Label = o.Label,
                    Compliance = Enum.TryParse<OverallCompliance>(o.Compliance, true, out var cp) ? cp : OverallCompliance.None,
                    Remark = o.Remark
                }).ToList();

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        static List<InspectionCarton> BuildCartons(PackagingRequest? pkg) =>
            (pkg?.Cartons ?? new())
                .Select((c, i) => new InspectionCarton
                {
                    CartonIndex     = i + 1,
                    CartonVariant   = c.CartonVariant?.Trim(),
                    Description     = c.Description?.Trim(),
                    PackingQuantity = c.PackingQuantity?.Trim(),
                    InnerPackingQty = c.InnerPackingQty,
                    InnerSizeL = c.InnerL, InnerSizeW = c.InnerW, InnerSizeH = c.InnerH, InnerWeight = c.InnerWeight,
                    OuterSizeL = c.OuterL, OuterSizeW = c.OuterW, OuterSizeH = c.OuterH, OuterWeight = c.OuterWeight
                }).ToList();

        // GET api/inspections/dashboard?year=2026&month=9&vendorId=&customerId=&category=
        // Quality Dashboard — mọi số liệu inspection lọc theo Period / Vendor / Customer / Category.
        [HttpGet("dashboard")]
        [AllowAnonymous]
        public async Task<IActionResult> Dashboard([FromQuery] int? year, [FromQuery] int? month,
            [FromQuery] string? vendorId, [FromQuery] string? customerId, [FromQuery] string? category)
        {
            try
            {
                var now   = DateTime.UtcNow;
                var y     = year  ?? now.Year;
                var m     = month ?? now.Month;
                var prevY = m == 1 ? y - 1 : y;
                var prevM = m == 1 ? 12 : m - 1;

                var all = await QualityStats.LoadAsync(_db);
                var custScope = await HttpContext.RequestServices.GetRequiredService<UserAccess>().CustomerScopeAsync();
                if (custScope != null)
                    all = all.Where(i => !string.IsNullOrEmpty(i.CustomerId) && custScope.Contains(i.CustomerId)).ToList();

                // Danh sách cho bộ lọc (lấy trước khi lọc)
                var filterVendors = all.Where(i => !string.IsNullOrEmpty(i.VendorId))
                    .GroupBy(i => i.VendorId!).Select(g => new { id = g.Key, name = g.First().VendorName })
                    .OrderBy(v => v.name).ToList();
                var filterCustomers = all.Where(i => !string.IsNullOrEmpty(i.CustomerId))
                    .GroupBy(i => i.CustomerId!).Select(g => new { id = g.Key, name = g.First().CustomerName })
                    .OrderBy(c => c.name).ToList();
                var filterCategories = all.Where(i => !string.IsNullOrEmpty(i.Category))
                    .Select(i => i.Category!).Distinct().OrderBy(c => c).ToList();

                var list = all.Where(i =>
                        (string.IsNullOrEmpty(vendorId)   || i.VendorId   == vendorId) &&
                        (string.IsNullOrEmpty(customerId) || i.CustomerId == customerId) &&
                        (string.IsNullOrEmpty(category)   || i.Category   == category))
                    .ToList();

                // CLC (Container Loading Check): theo Vendor/Customer; Category la thuoc tinh san pham nen loc Category thi khong tinh CLC
                var clcAll = string.IsNullOrEmpty(category)
                    ? (await ClcStats.LoadAsync(_db)).Where(c =>
                        (custScope == null || (c.CustomerCode != null && custScope.Contains(c.CustomerCode))) &&
                        (string.IsNullOrEmpty(vendorId)   || c.VendorCode   == vendorId) &&
                        (string.IsNullOrEmpty(customerId) || c.CustomerCode == customerId)).ToList()
                    : new List<ClcStat>();
                var clcYear = clcAll.Where(c => c.Date.Year == y).ToList();

                List<InspStat> InMonth(int yy, int mm) => list.Where(i => i.InspectionDate.Year == yy && i.InspectionDate.Month == mm).ToList();
                var cur  = InMonth(y, m);
                var prev = InMonth(prevY, prevM);
                var yearList = list.Where(i => i.InspectionDate.Year == y).ToList();
                int Shipped(IEnumerable<InspStat> l) => l.Where(i => i.IsCompleted && i.Type == InspectionType.PST).Sum(i => i.ShipmentQty);

                // ── Master data (không phụ thuộc bộ lọc) ──
                bool InCur(DateTime d) => d.Year == y && d.Month == m;
                var inspectorDates = await _userManager.Users.AsNoTracking()
                    .Where(u => u.InspectorId != null && u.InspectorId != "").Select(u => u.CreatedAt).ToListAsync();
                var productDates  = await _db.Products.AsNoTracking().Select(p => p.CreatedAt).ToListAsync();
                var vendorDates   = await _db.Vendors.AsNoTracking().Select(v => v.CreatedAt).ToListAsync();
                var customerDates = await _db.Customers.AsNoTracking().Select(c => c.CreatedAt).ToListAsync();

                // ── CAPA (Defect Management) ──
                var todayUtc = now.Date;
                var capas = (await _db.DefectCapas.AsNoTracking()
                        .Select(c => new { c.VendorId, c.VendorName, c.CustomerId, c.Status, c.DueDate, c.OpenDate })
                        .ToListAsync())
                    .Where(c => (custScope == null || custScope.Contains(c.CustomerId ?? "")) &&
                                (string.IsNullOrEmpty(vendorId)   || c.VendorId   == vendorId) &&
                                (string.IsNullOrEmpty(customerId) || c.CustomerId == customerId))
                    .ToList();
                bool CapaOpen(string st) => st != DefectCapaService.StatusClosed;
                var openCapaCount    = capas.Count(c => CapaOpen(c.Status));
                var overdueCapaCount = capas.Count(c => CapaOpen(c.Status) && c.DueDate != null && c.DueDate < todayUtc);

                var kpi = new
                {
                    totalInspections     = cur.Count,
                    totalInspectionsPrev = prev.Count,
                    awaiting             = cur.Count(i => i.IsAwaiting),
                    awaitingPrev         = prev.Count(i => i.IsAwaiting),
                    completed            = cur.Count(i => i.IsCompleted),
                    passRate             = QualityStats.PassRate(cur),
                    passRatePrev         = QualityStats.PassRate(prev),
                    failed               = cur.Count(i => i.Failed),
                    failedPrev           = prev.Count(i => i.Failed),
                    defectRate           = QualityStats.DefectRateAql(cur),
                    defectRatePrev       = QualityStats.DefectRateAql(prev),
                    openCapa             = (int?)openCapaCount,
                    overdueCapa          = (int?)overdueCapaCount,
                    inspectors           = inspectorDates.Count,
                    inspectorsNew        = inspectorDates.Count(InCur),
                    products             = productDates.Count,
                    productsNew          = productDates.Count(InCur),
                    vendors              = vendorDates.Count,
                    vendorsNew           = vendorDates.Count(InCur),
                    customers            = customerDates.Count,
                    customersNew         = customerDates.Count(InCur),
                    shippedQty           = Shipped(cur),
                    shippedQtyPrev       = Shipped(prev),
                };

                // ── 12 tháng của năm đang chọn ──
                var monthly = Enumerable.Range(1, 12).Select(mm =>
                {
                    var l    = yearList.Where(i => i.InspectionDate.Month == mm).ToList();
                    var done = l.Where(i => i.IsCompleted).ToList();
                    return new
                    {
                        month        = mm,
                        aqlQty       = done.Sum(i => i.SampleSize),
                        critical     = done.Sum(i => i.Critical),
                        major        = done.Sum(i => i.Major),
                        minor        = done.Sum(i => i.Minor),
                        totalDefects = done.Sum(i => i.Defects),
                        defectRate   = QualityStats.DefectRateAql(done),
                        ppi          = l.Count(i => i.Type == InspectionType.PPT),
                        dpi          = l.Count(i => i.Type == InspectionType.DPI),
                        psi          = l.Count(i => i.Type == InspectionType.PST),
                        clc          = ClcStats.InMonth(clcYear, mm),
                        total        = l.Count + ClcStats.InMonth(clcYear, mm),
                        completed    = done.Count,
                        awaiting     = l.Count(i => i.IsAwaiting),
                        shippedQty   = Shipped(l),
                    };
                }).ToList();

                // ── Top 5 sản phẩm lỗi / Top 5 shipment (tháng & năm) ──
                // Ảnh sản phẩm cho bảng Top 5 (khớp theo Product Code/ItemNumber, rồi tới tên)
                var productPhotos = await _db.Products.AsNoTracking()
                    .Where(p => p.PhotoUrl != null && p.PhotoUrl != "")
                    .Select(p => new { p.ItemNumber, p.ProductName, p.PhotoUrl }).ToListAsync();
                string? PhotoOf(InspStat i) =>
                    productPhotos.FirstOrDefault(p => !string.IsNullOrEmpty(i.ItemNumber) && p.ItemNumber == i.ItemNumber)?.PhotoUrl
                    ?? productPhotos.FirstOrDefault(p => p.ProductName == i.ProductName)?.PhotoUrl;

                object TopDefectProducts(List<InspStat> l)
                {
                    var done  = l.Where(i => i.IsCompleted && i.Defects > 0).ToList();
                    var total = done.Sum(i => i.Defects);
                    return done.GroupBy(i => i.ProductKey)
                        .Select(g => new
                        {
                            product    = g.First().ProductName,
                            itemNumber = g.First().ItemNumber,
                            photoUrl   = PhotoOf(g.First()),
                            mainDefect = QualityStats.TopDefect(g) ?? "—",
                            critical   = g.Sum(i => i.Critical),
                            major      = g.Sum(i => i.Major),
                            minor      = g.Sum(i => i.Minor),
                            defects    = g.Sum(i => i.Defects),
                            pct        = QualityStats.Pct(g.Sum(i => i.Defects), total)
                        })
                        .OrderByDescending(x => x.defects).ToList();   // client tự lấy Top 5 theo Severity
                }
                object TopShipments(List<InspStat> l)
                {
                    var pst   = l.Where(i => i.IsCompleted && i.Type == InspectionType.PST).ToList();
                    var total = pst.Sum(i => i.ShipmentQty);
                    return pst.GroupBy(i => i.ProductKey)
                        .Select(g => new
                        {
                            product    = g.First().ProductName,
                            itemNumber = g.First().ItemNumber,
                            photoUrl   = PhotoOf(g.First()),
                            qty        = g.Sum(i => i.ShipmentQty),
                            pct        = QualityStats.Pct(g.Sum(i => i.ShipmentQty), total)
                        })
                        .OrderByDescending(x => x.qty).Take(5).ToList();
                }

                // ── Supplier / Vendor performance (năm đang chọn) ──
                var vendorPerf = yearList.Where(i => !string.IsNullOrEmpty(i.VendorName))
                    .GroupBy(i => i.VendorId ?? i.VendorName)
                    .Select(g =>
                    {
                        var vm  = g.Where(i => i.InspectionDate.Month == m).ToList();
                        var vpm = list.Where(i => (i.VendorId ?? i.VendorName) == g.Key
                                               && i.InspectionDate.Year == prevY && i.InspectionDate.Month == prevM).ToList();
                        var judgedNow  = vm.Any(i => i.Judged);
                        var judgedPrev = vpm.Any(i => i.Judged);
                        var diff = judgedNow && judgedPrev ? QualityStats.PassRate(vm) - QualityStats.PassRate(vpm) : 0;
                        return new
                        {
                            vendorId    = g.First().VendorId,
                            vendorName  = g.First().VendorName,
                            inspections = g.Count(),
                            passRate    = QualityStats.PassRate(g),
                            defectRate  = QualityStats.DefectRateAql(g),
                            capaOverdue = (int?)capas.Count(c => (c.VendorId ?? c.VendorName) == g.Key && CapaOpen(c.Status)
                                                                 && c.DueDate != null && c.DueDate < todayUtc),
                            trend       = diff > 0.05 ? "up" : diff < -0.05 ? "down" : "flat"
                        };
                    })
                    .OrderByDescending(v => v.inspections).ToList();

                var recent = list.Select(i => new
                    {
                        jobNumber   = i.JobNumber,
                        date        = i.InspectionDate,
                        customer    = i.CustomerName,
                        vendor      = i.VendorName,
                        product     = i.ProductName,
                        stage       = StageCode(i.Type),
                        status      = i.Status.ToString(),
                        result      = i.FinalResult,
                        clcId       = (int?)null
                    })
                    .Concat(clcAll.Select(c => new
                    {
                        jobNumber   = (string?)c.JobNumber,
                        date        = c.Date,
                        customer    = c.CustomerName,
                        vendor      = c.VendorName,
                        product     = "—",
                        stage       = "CLC",
                        status      = c.Submitted ? "Completed" : "Pending",
                        result      = c.Overall == "PASS" ? "PASSED" : c.Overall == "FAIL" ? "FAILED" : c.Overall,
                        clcId       = (int?)c.Id
                    }))
                    .OrderByDescending(x => x.date).Take(8).ToList();

                object TopBy(Func<InspStat, string?> key, Func<InspStat, string?> name) => yearList
                    .Where(i => !string.IsNullOrEmpty(key(i)))
                    .GroupBy(key)
                    .Select(g => new { id = g.Key, name = name(g.First()), count = g.Count() })
                    .OrderByDescending(x => x.count).Take(5).ToList();

                return Ok(new
                {
                    updatedAt = now,
                    year = y, month = m,
                    filters = new { vendors = filterVendors, customers = filterCustomers, categories = filterCategories },
                    kpi,
                    monthly,
                    topDefectProductsMonth = TopDefectProducts(cur),
                    topDefectProductsYear  = TopDefectProducts(yearList),
                    topShipmentsMonth      = TopShipments(cur),
                    topShipmentsYear       = TopShipments(yearList),
                    vendorPerformance      = vendorPerf,
                    recentInspections      = recent,
                    topCustomers  = TopBy(i => i.CustomerId ?? i.CustomerName, i => i.CustomerName),
                    topInspectors = TopBy(i => i.InspectorId, i => i.InspectorName),
                    topVendors    = TopBy(i => i.VendorId ?? i.VendorName, i => i.VendorName),
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        static string StageCode(InspectionType t) => t switch
        {
            InspectionType.PPT => "PPI",
            InspectionType.DPI => "DPI",
            _                  => "PSI"
        };

        // GET api/inspections/inspector-review/{inspectorId}?year=2026&month=4
        [HttpGet("inspector-review/{inspectorId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInspectorReview(string inspectorId, [FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var now = DateTime.UtcNow;
                var targetYear  = year  ?? now.Year;
                var targetMonth = month ?? now.Month;

                var allInsp  = await QualityStats.LoadAsync(_db, q => q.Where(i => i.InspectorId == inspectorId));
                // CLC luu InspectorId = AppUser.Id -> doi tu ma inspector (vd. IP10003) sang user id
                var inspUserId = await _userManager.Users.AsNoTracking().Where(u => u.InspectorId == inspectorId).Select(u => u.Id).FirstOrDefaultAsync();
                var clcAll   = inspUserId == null ? new List<ClcStat>() : await ClcStats.LoadAsync(_db, q => q.Where(c => c.InspectorId == inspUserId));
                var clcYear  = clcAll.Where(c => c.Date.Year == targetYear).ToList();
                var yearInsp = allInsp.Where(i => i.InspectionDate.Year == targetYear).ToList();
                var monthInsp = yearInsp.Where(i => i.InspectionDate.Month == targetMonth).ToList();

                // ── Monthly stacked bar (năm đang chọn) ──
                var monthlyData = Enumerable.Range(1, 12).Select(m => new
                {
                    month = m,
                    ppt = yearInsp.Count(i => i.InspectionDate.Month == m && i.Type == InspectionType.PPT),
                    dpi = yearInsp.Count(i => i.InspectionDate.Month == m && i.Type == InspectionType.DPI),
                    pst = yearInsp.Count(i => i.InspectionDate.Month == m && i.Type == InspectionType.PST),
                    clc = ClcStats.InMonth(clcYear, m),
                }).ToList();

                // ── CLC Pass/Fail (chi job da gui duyet co ket luan) ──
                var clcPassed = clcYear.Count(c => c.Passed);
                var clcFailed = clcYear.Count(c => c.Failed);
                var clcHeld   = clcYear.Count(c => c.Held);
                var clcByMonth = Enumerable.Range(1, 12).Select(m => new
                {
                    month  = m,
                    passed = clcYear.Count(c => c.Date.Month == m && c.Passed),
                    failed = clcYear.Count(c => c.Date.Month == m && c.Failed),
                    held   = clcYear.Count(c => c.Date.Month == m && c.Held),
                }).ToList();

                // Nam truoc (so sanh the KPI) + Latest Inspections (gop inspection san pham va CLC)
                var prevInsp = allInsp.Where(i => i.InspectionDate.Year == targetYear - 1).ToList();
                var prevClc  = clcAll.Count(c => c.Date.Year == targetYear - 1);
                var latest = allInsp.Select(i => new { id = i.Id, clcId = (int?)null, date = i.InspectionDate, type = StageCode(i.Type),
                                    customer = i.CustomerName, vendor = i.VendorName, jobNumber = i.JobNumber,
                                    result = i.IsCompleted ? (i.FinalResult == "PASSED" ? "PASS" : i.FinalResult == "FAILED" ? "FAIL" : "DONE") : "PENDING" })
                    .Concat(clcAll.Select(c => new { id = c.Id, clcId = (int?)c.Id, date = c.Date, type = "CLC",
                                    customer = c.CustomerName, vendor = c.VendorName, jobNumber = (string?)c.JobNumber,
                                    result = c.Submitted ? (c.Overall ?? "DONE") : "PENDING" }))
                    .OrderByDescending(x => x.date).Take(8).ToList();

                // ── PST Pass/Fail (chỉ tính inspection có kết luận PASSED/FAILED → tổng = 100%) ──
                var pstYear   = yearInsp.Where(i => i.Type == InspectionType.PST).ToList();
                var pstPassed = pstYear.Count(i => i.Passed);
                var pstFailed = pstYear.Count(i => i.Failed);
                var pstByMonth = Enumerable.Range(1, 12).Select(m => new
                {
                    month  = m,
                    passed = pstYear.Count(i => i.InspectionDate.Month == m && i.Passed),
                    failed = pstYear.Count(i => i.InspectionDate.Month == m && i.Failed),
                }).ToList();

                // ── On-time (tháng đang chọn + cả năm) ──
                var monthKnown  = monthInsp.Where(i => i.OnTime.HasValue).ToList();
                var onTimeCount = monthKnown.Count(i => i.OnTime == true);
                var delayCount  = monthKnown.Count - onTimeCount;

                // ── Annual Inspection Performance (5 năm) ──
                var annual = QualityStats.AnnualYears(allInsp).Select(yy =>
                {
                    var l    = allInsp.Where(i => i.InspectionDate.Year == yy).ToList();
                    var done = l.Where(i => i.IsCompleted).ToList();
                    var cy = clcAll.Where(c => c.Date.Year == yy).ToList();
                    return new
                    {
                        year                = yy,
                        totalInspection     = l.Count + cy.Count,
                        ppi                 = l.Count(i => i.Type == InspectionType.PPT),
                        dpi                 = l.Count(i => i.Type == InspectionType.DPI),
                        psi                 = l.Count(i => i.Type == InspectionType.PST),
                        clc                 = cy.Count,
                        defectDetectionRate = done.Any() ? QualityStats.DefectRateAql(done) : (double?)null,
                        onTimeRate          = QualityStats.OnTimeRate(l),
                    };
                }).ToList();

                return Ok(new
                {
                    inspectorId,
                    targetYear,
                    targetMonth,
                    yearTotal   = yearInsp.Count + clcYear.Count,
                    monthTotal  = monthInsp.Count,
                    yearPpt     = yearInsp.Count(i => i.Type == InspectionType.PPT),
                    yearDpi     = yearInsp.Count(i => i.Type == InspectionType.DPI),
                    yearPst     = yearInsp.Count(i => i.Type == InspectionType.PST),
                    yearClc     = clcYear.Count,
                    clcPassed,
                    clcFailed,
                    clcHeld,
                    clcPassRate = QualityStats.Pct(clcPassed, clcPassed + clcFailed + clcHeld),
                    clcFailRate = QualityStats.Pct(clcFailed, clcPassed + clcFailed + clcHeld),
                    clcHoldRate = QualityStats.Pct(clcHeld, clcPassed + clcFailed + clcHeld),
                    prevTotal = prevInsp.Count + prevClc,
                    prevPpt = prevInsp.Count(i => i.Type == InspectionType.PPT),
                    prevDpi = prevInsp.Count(i => i.Type == InspectionType.DPI),
                    prevPst = prevInsp.Count(i => i.Type == InspectionType.PST),
                    prevClc,
                    latest,
                    clcByMonth,
                    yearPassed  = yearInsp.Count(i => i.Passed),
                    yearFailed  = yearInsp.Count(i => i.Failed),
                    yearPassRate = QualityStats.PassRate(yearInsp),
                    pstPassed,
                    pstFailed,
                    pstPassRate = QualityStats.Pct(pstPassed, pstPassed + pstFailed),
                    pstFailRate = pstPassed + pstFailed > 0 ? Math.Round(100 - QualityStats.Pct(pstPassed, pstPassed + pstFailed), 1) : 0,
                    onTimeCount,
                    delayedCount = delayCount,
                    onTimeRate   = QualityStats.Pct(onTimeCount, monthKnown.Count),
                    delayedRate  = monthKnown.Count > 0 ? Math.Round(100 - QualityStats.Pct(onTimeCount, monthKnown.Count), 1) : 0,
                    onTimeKnown  = monthKnown.Count,
                    onTimeYearRate = QualityStats.OnTimeRate(yearInsp),
                    monthlyData,
                    pstByMonth,
                    annual,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // PUT api/inspections/{id}
        [HttpPut("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Update(int id, [FromBody] CreateInspectionRequest request)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.JobRequest, PermAction.Edit)) is { } denied) return denied;
            try
            {
                var inspection = await _db.Inspections
                    .IgnoreQueryFilters()
                    .Include(i => i.Steps)
                    .Include(i => i.OverallConclusions)
                    .Include(i => i.Packaging)
                    .Include(i => i.ProductSpec)
                    .Include(i => i.ColourSwatches)
                    .Include(i => i.PerformanceTests)
                    .Include(i => i.References)
                    .Include(i => i.Cartons)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (inspection == null) return NotFound();

                // Chỉ cho phép edit khi: New(Pending=0), OnGoing(InProgress=1), Pending(Completed=2 theo enum cũ)
                var editableStatuses = new[]
                {
                InspectionStatus.Pending,
                InspectionStatus.InProgress,
                InspectionStatus.Completed,
                InspectionStatus.Draft
            };
                if (!editableStatuses.Contains(inspection.Status))
                    return BadRequest(new { error = "Cannot edit an inspection with status Completed or Cancelled." });

                // Job nháp: "Assign & Send to Inspector" (IsDraft=false) → chuyển sang New; vẫn lưu nháp thì giữ Draft.
                // Job đã giao không quay lại Draft được.
                if (inspection.Status == InspectionStatus.Draft && request.IsDraft == false)
                    inspection.Status = InspectionStatus.Pending;

                var inspType = Enum.TryParse<InspectionType>(request.InspectionType, true, out var parsedType)
                    ? parsedType : InticooInspection.Domain.Entities.InspectionType.DPI;

                var aqlLevel = Enum.TryParse<AqlInspectionLevel>(request.AqlLevel, true, out var parsedAqlLevel)
                    ? parsedAqlLevel : AqlInspectionLevel.II;

                static DefectAqlLevel ParseDefectAql(string? val) => val switch
                {
                    "0.065" => DefectAqlLevel.AQL_0_065,
                    "0.10" or "0.1" => DefectAqlLevel.AQL_0_1,
                    "0.15" => DefectAqlLevel.AQL_0_15,
                    "0.25" => DefectAqlLevel.AQL_0_25,
                    "0.40" or "0.4" => DefectAqlLevel.AQL_0_4,
                    "0.65" => DefectAqlLevel.AQL_0_65,
                    "1.0" => DefectAqlLevel.AQL_1_0,
                    "1.5" => DefectAqlLevel.AQL_1_5,
                    "2.5" => DefectAqlLevel.AQL_2_5,
                    "4.0" => DefectAqlLevel.AQL_4_0,
                    "6.5" => DefectAqlLevel.AQL_6_5,
                    _ => DefectAqlLevel.NotAllowed
                };
                static PackagingType? ParsePkgType(string? v) => v switch
                {
                    "FSC Carton" => PackagingType.FSCCarton,
                    "Non-FSC Carton" => PackagingType.NonFSCCarton,
                    _ => null
                };
                static CartonColor? ParseCartonColor(string? v) => v switch
                {
                    "White" => CartonColor.White,
                    "Brown" => CartonColor.Brown,
                    _ => null
                };
                static CardboardType? ParseCardboard(string? v) => v switch
                {
                    "Single face" => CardboardType.SingleFace,
                    "Single Wall" => CardboardType.SingleWall,
                    "Double Wall" => CardboardType.DoubleWall,
                    _ => null
                };
                static ShippingMarkType? ParseShipping(string? v) => v switch
                {
                    "Color Label" => ShippingMarkType.ColorLabel,
                    "Printing" => ShippingMarkType.Printing,
                    _ => null
                };

                // ── General Info ──
                inspection.Title = request.Title;
                inspection.Description = request.Description;
                inspection.CustomerName = request.CustomerName;
                inspection.CustomerId = request.CustomerId;
                inspection.VendorName = request.VendorName;
                inspection.VendorId = request.VendorId;
                inspection.InspectionLocation = request.InspectionLocation;
                inspection.PoNumber = request.PoNumber;
                inspection.InspectionDate = request.InspectionDate ?? inspection.InspectionDate;
                inspection.ItemNumber = request.ItemNumber;
                inspection.InspectionType = inspType;
                inspection.ProductName = request.ProductName;
                inspection.ProductCategory = request.ProductCategory;
                inspection.TotalShipmentQty = request.TotalShipmentQty;
                inspection.TotalCartonBoxes = request.TotalCartonBoxes;
                inspection.GeneralRemark = request.GeneralRemark;
                inspection.InspectorId = request.InspectorId;
                inspection.InspectorName = request.InspectorName;
                inspection.InspectionStartTime = request.InspectionStartTime;
                inspection.InspectionEndTime = request.InspectionEndTime;
                inspection.InspectorRemarks = request.InspectorRemarks is { Length: > 500 } rm ? rm[..500] : request.InspectorRemarks;
                inspection.Photo1Url = request.Photo1Url;
                inspection.Photo2Url = request.Photo2Url;

                // ── AQL ──
                inspection.AqlInspectionLevel = aqlLevel;
                inspection.CriticalAql = ParseDefectAql(request.AqlCritical);
                inspection.MajorAql = ParseDefectAql(request.AqlMajor);
                inspection.MinorAql = ParseDefectAql(request.AqlMinor);
                inspection.CriticalSampleSize = request.AqlCriticalSampleSize;
                inspection.CriticalAccept = request.AqlCriticalAccept;
                inspection.CriticalReject = request.AqlCriticalReject;
                inspection.MajorSampleSize = request.AqlMajorSampleSize;
                inspection.MajorAccept = request.AqlMajorAccept;
                inspection.MajorReject = request.AqlMajorReject;
                inspection.MinorSampleSize = request.AqlMinorSampleSize;
                inspection.MinorAccept = request.AqlMinorAccept;
                inspection.MinorReject = request.AqlMinorReject;

                // ── Packaging ──
                if (request.Packaging != null)
                {
                    if (inspection.Packaging == null)
                        inspection.Packaging = new InspectionPackaging { InspectionId = id };

                    inspection.Packaging.ItemNumber = request.Packaging.ItemNumber;
                    inspection.Packaging.CartonNumber = request.Packaging.CartonNumber;
                    inspection.Packaging.PackagingType = ParsePkgType(request.Packaging.PackagingType);
                    inspection.Packaging.CartonColor = ParseCartonColor(request.Packaging.CartonColor);
                    inspection.Packaging.CardboardType = ParseCardboard(request.Packaging.CardboardType);
                    inspection.Packaging.ShippingMark = ParseShipping(request.Packaging.ShippingMark);
                    inspection.Packaging.HasBarcode = request.Packaging.HasBarcode;
                    inspection.Packaging.InnerPackingQty = request.Packaging.InnerPackingQty;
                    inspection.Packaging.InnerSizeL = request.Packaging.InnerL;
                    inspection.Packaging.InnerSizeW = request.Packaging.InnerW;
                    inspection.Packaging.InnerSizeH = request.Packaging.InnerH;
                    inspection.Packaging.InnerWeight = request.Packaging.InnerWeight;
                    inspection.Packaging.InnerPackingRemark = request.Packaging.InnerPackingRemark;
                    inspection.Packaging.OuterSizeL = request.Packaging.OuterL;
                    inspection.Packaging.OuterSizeW = request.Packaging.OuterW;
                    inspection.Packaging.OuterSizeH = request.Packaging.OuterH;
                    inspection.Packaging.OuterWeight = request.Packaging.OuterWeight;
                    inspection.Packaging.AssemblyInstruction = request.Packaging.AssemblyInstruction;
                    inspection.Packaging.Hardware = request.Packaging.Hardware;
                    inspection.Packaging.BurstingTest = request.Packaging.BurstingTest;

                    // Carton list: thay toàn bộ theo request (số dòng = Carton Number)
                    _db.InspectionCartons.RemoveRange(inspection.Cartons);
                    foreach (var c in BuildCartons(request.Packaging))
                    {
                        c.InspectionId = id;
                        _db.InspectionCartons.Add(c);
                    }
                }

                // ── Product Spec ──
                if (request.ProductSpec != null)
                {
                    if (inspection.ProductSpec == null)
                        inspection.ProductSpec = new InspectionProductSpec { InspectionId = id };

                    inspection.ProductSpec.SizeL = request.ProductSpec.SizeL;
                    inspection.ProductSpec.SizeW = request.ProductSpec.SizeW;
                    inspection.ProductSpec.SizeH = request.ProductSpec.SizeH;
                    inspection.ProductSpec.Weight = request.ProductSpec.Weight;
                    inspection.ProductSpec.CompareGoldenSample = request.ProductSpec.CompareGoldenSample;
                }

                // ── Colour Swatches (replace) ──
                _db.RemoveRange(inspection.ColourSwatches);
                inspection.ColourSwatches = (request.ProductSpec?.ColourSwatches ?? Array.Empty<ColourSwatchRequest>())
                    .Select((c, i) => new InspectionColourSwatch
                    {
                        Order    = i + 1,
                        Material = c.Material ?? "",
                        Remark   = c.Remark   ?? ""
                    })
                    .ToList();

                // ── Performance Tests (replace) ──
                _db.RemoveRange(inspection.PerformanceTests);
                inspection.PerformanceTests = (request.PerformanceTests ?? new())
                    .Select((t, i) => new InspectionPerformanceTest
                    {
                        Order = i + 1,
                        MasterId = t.MasterId,
                        Category = t.Category,
                        TestItem = t.TestItem,
                        TestProtocol = t.TestProtocol,
                        TestRequirement = t.TestRequirement,
                        Remark = t.Remark
                    }).ToList();

                // ── References (replace) ──
                _db.RemoveRange(inspection.References);
                inspection.References = (request.References ?? new())
                    .Select((r, i) => new InspectionReference
                    {
                        Order = i + 1,
                        ReferenceName = r.ReferenceName,
                        FileName = r.FileName,
                        FileUrl = r.FileUrl,
                        Remark = r.Remark
                    }).ToList();

                // ── Overall Conclusions (replace) ──
                _db.RemoveRange(inspection.OverallConclusions);
                inspection.OverallConclusions = request.OverallConclusions
                    .Select((o, i) => new InspectionOverallConclusion
                    {
                        InspectionId = id,
                        Order = i + 1,
                        Letter = o.Letter,
                        Label = o.Label,
                        Compliance = Enum.TryParse<OverallCompliance>(o.Compliance, true, out var cp)
                                       ? cp : OverallCompliance.None,
                        Remark = o.Remark
                    }).ToList();

                await _db.SaveChangesAsync();
                return Ok(new { success = true, id = inspection.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PUT api/inspections/{id}/qc-result
        // Lưu toàn bộ kết quả kiểm hàng từ màn hình QC wizard
        // Toàn bộ QC data lưu vào QcResultJson (JSON string) trên Inspection
        // + cập nhật OverallConclusions, Status, CompletedAt
        // ═══════════════════════════════════════════════════════════════
        /// <summary>Ghi kích thước thực đo (C-i Product Size) vào cột riêng để truy vấn/báo cáo; 0/rỗng = chưa đo → null.</summary>
        private static void SyncActualSize(Inspection inspection, QcProductSpecResultDto? ps)
        {
            if (ps == null) return;
            static double? Pos(double? v) => v is > 0 ? v : null;
            inspection.ProductSpec ??= new InspectionProductSpec { InspectionId = inspection.Id };
            inspection.ProductSpec.ActualSizeL = Pos(ps.SpecL);
            inspection.ProductSpec.ActualSizeW = Pos(ps.SpecW);
            inspection.ProductSpec.ActualSizeH = Pos(ps.SpecH);
        }

        [HttpPut("{id}/qc-result")]
        [AllowAnonymous]
        public async Task<IActionResult> SaveQcResult(int id, [FromBody] QcResultRequest request)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.InspectionSchedule, PermAction.Use), (ModuleKeys.InspectionSummary, PermAction.Create), (ModuleKeys.InspectionSummary, PermAction.Edit)) is { } denied) return denied;
            try
            {
                var inspection = await _db.Inspections
                    .Include(i => i.OverallConclusions)
                    .Include(i => i.ProductSpec)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (inspection == null) return NotFound(new { error = "Inspection not found" });
                SyncActualSize(inspection, request.ProductSpec);

                // ════════════════════════════════════════════════════════
                // 1. Các field quan trọng — lưu thẳng vào DB column riêng
                //    để query/filter/report nhanh, không cần parse JSON
                // ════════════════════════════════════════════════════════
                inspection.FinalResult = request.FinalResult;       // "PASSED"|"FAILED"|"NA"
                inspection.SignatureUrl = request.SignatureUrl;
                inspection.InspectorComments = request.InspectorComments;
                inspection.QcInspectionRef = request.InspectionReference;
                inspection.InspectionLocation = request.InspectionLocation ?? inspection.InspectionLocation;
                inspection.InspectionDate = request.InspectionDate ?? inspection.InspectionDate;
                if (!string.IsNullOrEmpty(request.Photo1Url)) inspection.Photo1Url = request.Photo1Url;
                if (!string.IsNullOrEmpty(request.Photo2Url)) inspection.Photo2Url = request.Photo2Url;
                inspection.Status = InspectionStatus.Completed;
                inspection.CompletedAt = DateTime.UtcNow;

                // ════════════════════════════════════════════════════════
                // 2. Serialize QcResultJson — KHÔNG chứa các field trên
                //    để tránh trùng lặp, chỉ chứa QC data chi tiết
                // ════════════════════════════════════════════════════════
                var qcData = new
                {
                    schemaVersion = request.SchemaVersion,
                    inspectionStartTime = request.InspectionStartTime,
                    overallConclusions = request.OverallConclusions,
                    quantityConformity = request.QuantityConformity,
                    packaging = request.Packaging,
                    productSpec = request.ProductSpec,
                    aql = request.Aql,
                    performanceTests = request.PerformanceTests,
                    defects = request.Defects,
                    lastStep = request.LastStep,
                    stepSchema = request.StepSchema,
                };
                inspection.QcResultJson = System.Text.Json.JsonSerializer.Serialize(qcData,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

                // ════════════════════════════════════════════════════════
                // 3. Overall Conclusions — lưu vào bảng riêng để query
                // ════════════════════════════════════════════════════════
                if (request.OverallConclusions?.Any() == true)
                {
                    _db.RemoveRange(inspection.OverallConclusions);
                    inspection.OverallConclusions = request.OverallConclusions
                        .Select((o, i) => new InspectionOverallConclusion
                        {
                            InspectionId = id,
                            Order = i + 1,
                            Letter = o.Letter,
                            Label = o.Label,
                            Compliance = Enum.TryParse<OverallCompliance>(o.Compliance, true, out var cp)
                                             ? cp : OverallCompliance.None,
                            Remark = o.Remark
                        }).ToList();
                }

                await _db.SaveChangesAsync();

                // Defect Management: tự mở CAPA cho nhóm lỗi vượt ngưỡng
                // (Critical ≥1 / Major ≥3 / Minor ≥10 pcs cùng mã lỗi). Lỗi ở đây không chặn việc lưu kết quả.
                try { await DefectCapaService.AutoOpenAsync(_db, inspection.Id); }
                catch (Exception capaEx) { Console.Error.WriteLine($"[CAPA] Auto-open failed for #{inspection.Id}: {capaEx.Message}"); }

                // ════════════════════════════════════════════════════════
                // 4. Gửi email thông báo cho Customer (nếu có email).
                //    Chạy nền với scope DI RIÊNG — KHÔNG dùng _db của request vì
                //    request kết thúc sẽ dispose _db. Gửi thành công thì set
                //    TinhTrangGuiMail = 1 để InspectionDoneMailWorker không gửi lại.
                //    Nếu gửi hụt, cờ giữ = 0 và worker 10 phút sẽ gửi lại.
                // ════════════════════════════════════════════════════════
                var inspectionId = inspection.Id;
                var tenantInfo   = HttpContext.RequestServices.GetRequiredService<InticooInspection.API.Services.Tenancy.TenantContext>().Tenant;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(inspection.CustomerId)) return;

                        using var scope = _scopeFactory.CreateScope();
                        // Scope mới mặc định nối DB gốc → gắn lại công ty của request (DB riêng của khách)
                        scope.ServiceProvider.GetRequiredService<InticooInspection.API.Services.Tenancy.TenantContext>().Set(tenantInfo);
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        var customer = await db.Customers.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CustomerId == inspection.CustomerId);
                        if (customer == null || string.IsNullOrEmpty(customer.Email)) return;

                        await SendCompletionEmailAsync(inspection, customer.Email, customer.AlternateReportEmail);

                        // Đánh dấu đã gửi để worker 10 phút không gửi trùng
                        await db.Inspections
                            .Where(x => x.Id == inspectionId)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TinhTrangGuiMail, 1));
                    }
                    catch (Exception emailEx)
                    {
                        // Gửi hụt không ảnh hưởng response; worker 10 phút sẽ thử lại (cờ vẫn = 0)
                        Console.WriteLine($"[SendCompletionEmail] Lỗi gửi mail Done: {emailEx.Message}");
                        Console.WriteLine($"[SendCompletionEmail] Stack: {emailEx.StackTrace}");
                    }
                });

                return Ok(new { success = true, id = inspection.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PUT api/inspections/{id}/qc-result/draft
        // Lưu DRAFT QC data — dùng cho chức năng "Review" (xem trước báo cáo
        // trước khi bấm Done). KHÔNG đổi status, KHÔNG set CompletedAt,
        // KHÔNG gửi email. Inspector có thể review nhiều lần mà không gây
        // side effect.
        // ═══════════════════════════════════════════════════════════════
        [HttpPut("{id}/qc-result/draft")]
        [AllowAnonymous]
        public async Task<IActionResult> SaveQcResultDraft(int id, [FromBody] QcResultRequest request)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.InspectionSchedule, PermAction.Use), (ModuleKeys.InspectionSummary, PermAction.Create), (ModuleKeys.InspectionSummary, PermAction.Edit)) is { } denied) return denied;
            try
            {
                var inspection = await _db.Inspections
                    .Include(i => i.OverallConclusions)
                    .Include(i => i.ProductSpec)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (inspection == null) return NotFound(new { error = "Inspection not found" });
                SyncActualSize(inspection, request.ProductSpec);

                // Cập nhật các field thường lưu (không đổi Status/CompletedAt)
                inspection.FinalResult       = request.FinalResult;
                inspection.SignatureUrl      = request.SignatureUrl;
                inspection.InspectorComments = request.InspectorComments;
                inspection.QcInspectionRef   = request.InspectionReference;
                inspection.InspectionLocation = request.InspectionLocation ?? inspection.InspectionLocation;
                inspection.InspectionDate    = request.InspectionDate ?? inspection.InspectionDate;
                if (!string.IsNullOrEmpty(request.Photo1Url)) inspection.Photo1Url = request.Photo1Url;
                if (!string.IsNullOrEmpty(request.Photo2Url)) inspection.Photo2Url = request.Photo2Url;
                // KHÔNG set Status = Completed, KHÔNG set CompletedAt — đây là draft

                // Serialize QcResultJson — giống endpoint chính
                var qcData = new
                {
                    schemaVersion       = request.SchemaVersion,
                    inspectionStartTime = request.InspectionStartTime,
                    overallConclusions  = request.OverallConclusions,
                    quantityConformity = request.QuantityConformity,
                    packaging          = request.Packaging,
                    productSpec        = request.ProductSpec,
                    aql                = request.Aql,
                    performanceTests   = request.PerformanceTests,
                    defects            = request.Defects,
                    lastStep            = request.LastStep,
                    stepSchema          = request.StepSchema,
                };
                inspection.QcResultJson = System.Text.Json.JsonSerializer.Serialize(qcData,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

                // Overall Conclusions — cập nhật để Report đọc đúng dữ liệu khi preview
                if (request.OverallConclusions?.Any() == true)
                {
                    _db.RemoveRange(inspection.OverallConclusions);
                    inspection.OverallConclusions = request.OverallConclusions
                        .Select((o, i) => new InspectionOverallConclusion
                        {
                            InspectionId = id,
                            Order        = i + 1,
                            Letter       = o.Letter,
                            Label        = o.Label,
                            Compliance   = Enum.TryParse<OverallCompliance>(o.Compliance, true, out var cp)
                                              ? cp : OverallCompliance.None,
                            Remark       = o.Remark
                        }).ToList();
                }

                await _db.SaveChangesAsync();

                return Ok(new { success = true, draft = true, id = inspection.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        // ════════════════════════════════════════════════════════
        // Helper: Gửi email hoàn thành inspection cho Customer
        // Dùng chung cấu hình MailSettings:* với mail vendor hàng tuần.
        // (KHÔNG đọc section "Email:" cũ nữa để tránh phải cấu hình 2 nơi)
        // ════════════════════════════════════════════════════════
        private async Task SendCompletionEmailAsync(Inspection inspection, string toEmail, string? ccEmail = null)
        {
            // Đọc cùng cấu hình SMTP với mail vendor hàng tuần (no-reply@inticoo.com)
            var smtp     = _config["MailSettings:SmtpHost"] ?? "smtp.office365.com";
            var port     = _config.GetValue<int>("MailSettings:SmtpPort", 587);
            var user     = _config["MailSettings:Username"] ?? "";
            var pass     = _config["MailSettings:Password"] ?? "";
            var from     = _config["MailSettings:SenderEmail"] ?? user;
            var fromName = _config["MailSettings:SenderName"] ?? "Inticoo Global Services";
            var useSsl   = _config.GetValue<bool>("MailSettings:UseSsl", true);
            var baseUrl  = _config["Email:ReportBaseUrl"] ?? "https://inticoo.com";

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                throw new InvalidOperationException(
                    "MailSettings is not fully configured (Username/Password empty)");

            var completedDate = (inspection.CompletedAt ?? DateTime.UtcNow).ToString("d MMMM yyyy");
            var inspType = inspection.InspectionType.ToString() switch
            {
                "DPI" => "During-Production Inspection",
                "PPT" => "Pre-Production Inspection",
                "PST" => "Pre-Shipment Inspection (Final Inspection)",
                _ => inspection.InspectionType.ToString()
            };
            var result = inspection.FinalResult ?? "N/A";
            var reportLink = $"{baseUrl}/inspection-report/{inspection.Id}";

            // Màu badge theo result
            var resultColor = result.ToUpper() switch
            {
                "PASS" or "APPROVED" or "ACCEPTED" => "#28a745",
                "FAIL" or "REJECTED" => "#dc3545",
                "PENDING" or "HOLD" => "#ffc107",
                _ => "#6c757d"
            };

            var subject = $"[Inticoo] Inspection Report Completed - {inspection.JobNumber}";

            // Body HTML đẹp - sender Microsoft 365 sẽ render đúng
            var body = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>Inspection Completed</title></head>
<body style='font-family:Segoe UI,Arial,sans-serif;color:#333;background:#f5f7fb;padding:20px;margin:0;'>
  <div style='max-width:680px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);'>
    <div style='background:linear-gradient(135deg,#1e6091 0%,#2a9d8f 100%);color:#fff;padding:24px;'>
      <h2 style='margin:0;font-size:22px;'>✅ Inspection Report Completed</h2>
      <p style='margin:6px 0 0;opacity:.9;font-size:14px;'>Inticoo Inspection System</p>
    </div>
    <div style='padding:24px;'>
      <p>Dear Sir/Madam,</p>
      <p>This is a notification that the inspection report has been completed on <strong>{completedDate}</strong>.</p>
      <p>Please find the inspection details below:</p>

      <table style='width:100%;border-collapse:collapse;margin-top:16px;font-size:14px;'>
        <tr>
          <td style='padding:8px 12px;background:#f8f9fa;border:1px solid #e9ecef;width:35%;'><strong>Job No</strong></td>
          <td style='padding:8px 12px;border:1px solid #e9ecef;'>{System.Net.WebUtility.HtmlEncode(inspection.JobNumber ?? "—")}</td>
        </tr>
        <tr>
          <td style='padding:8px 12px;background:#f8f9fa;border:1px solid #e9ecef;'><strong>Category</strong></td>
          <td style='padding:8px 12px;border:1px solid #e9ecef;'>{System.Net.WebUtility.HtmlEncode(inspection.ProductCategory ?? "—")}</td>
        </tr>
        <tr>
          <td style='padding:8px 12px;background:#f8f9fa;border:1px solid #e9ecef;'><strong>Product Name</strong></td>
          <td style='padding:8px 12px;border:1px solid #e9ecef;'>{System.Net.WebUtility.HtmlEncode(inspection.ProductName ?? "—")}</td>
        </tr>
        <tr>
          <td style='padding:8px 12px;background:#f8f9fa;border:1px solid #e9ecef;'><strong>Item Number</strong></td>
          <td style='padding:8px 12px;border:1px solid #e9ecef;'>{System.Net.WebUtility.HtmlEncode(inspection.ItemNumber ?? "—")}</td>
        </tr>
        <tr>
          <td style='padding:8px 12px;background:#f8f9fa;border:1px solid #e9ecef;'><strong>Inspection Type</strong></td>
          <td style='padding:8px 12px;border:1px solid #e9ecef;'>{System.Net.WebUtility.HtmlEncode(inspType)}</td>
        </tr>
        <tr>
          <td style='padding:8px 12px;background:#f8f9fa;border:1px solid #e9ecef;'><strong>Vendor Name</strong></td>
          <td style='padding:8px 12px;border:1px solid #e9ecef;'>{System.Net.WebUtility.HtmlEncode(inspection.VendorName ?? "—")}</td>
        </tr>
        <tr>
          <td style='padding:8px 12px;background:#f8f9fa;border:1px solid #e9ecef;'><strong>Vendor ID</strong></td>
          <td style='padding:8px 12px;border:1px solid #e9ecef;'>{System.Net.WebUtility.HtmlEncode(inspection.VendorId ?? "—")}</td>
        </tr>
        <tr>
          <td style='padding:8px 12px;background:#f8f9fa;border:1px solid #e9ecef;'><strong>Result</strong></td>
          <td style='padding:8px 12px;border:1px solid #e9ecef;'>
            <span style='display:inline-block;padding:4px 12px;background:{resultColor};color:#fff;border-radius:4px;font-weight:600;'>{System.Net.WebUtility.HtmlEncode(result)}</span>
          </td>
        </tr>
      </table>

      <div style='margin-top:24px;text-align:center;'>
        <a href='{reportLink}' style='display:inline-block;padding:12px 28px;background:#1e6091;color:#fff;text-decoration:none;border-radius:6px;font-weight:600;'>
          📄 Download Inspection Report
        </a>
      </div>
      <p style='margin-top:16px;font-size:13px;color:#666;text-align:center;'>
        Or copy this link: <br/>
        <a href='{reportLink}' style='color:#1e6091;word-break:break-all;'>{reportLink}</a>
      </p>

      <p style='margin-top:24px;'>Should you have any questions or require further clarification, please feel free to contact us.</p>
      <p>Best regards,<br/><strong>Inticoo Global Services</strong></p>
    </div>
    <div style='padding:14px 24px;background:#f5f7fb;color:#888;font-size:12px;text-align:center;border-top:1px solid #eee;'>
      <a href='https://inticoo.com' style='color:#888;'>www.inticoo.com</a> &nbsp;|&nbsp; This is an automated email from Inticoo Inspection System.
    </div>
  </div>
</body></html>";

            using var client = new SmtpClient(smtp, port)
            {
                Credentials    = new NetworkCredential(user, pass),
                EnableSsl      = useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout        = 30000
            };

            using var msg = new MailMessage
            {
                From            = new MailAddress(from, fromName),
                Subject         = subject,
                Body            = body,
                IsBodyHtml      = true,
                BodyEncoding    = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8
            };
            msg.To.Add(toEmail);

            // CC AlternateReportEmail của customer — hỗ trợ NHIỀU email phân tách bằng
            // ; , khoảng trắng / xuống dòng. Bỏ qua địa chỉ không hợp lệ, trùng To, trùng nhau.
            if (!string.IsNullOrWhiteSpace(ccEmail))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { toEmail.Trim() };
                var ccAddrs = ccEmail
                    .Split(new[] { ';', ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => a.Contains('@') && seen.Add(a));
                foreach (var addr in ccAddrs)
                    msg.CC.Add(addr);
            }

            await client.SendMailAsync(msg);
        }
    } // end class InspectionController

    public class CreateInspectionRequest
    {
        /// <summary>true = lưu nháp (Draft, chưa giao inspector); false khi job đang Draft = "Assign &amp; Send" → New.
        /// null (client cũ) = giữ nguyên trạng thái.</summary>
        public bool?     IsDraft            { get; set; }
        public string    Title              { get; set; } = "";
        public string?   Description        { get; set; }
        public string    CustomerName       { get; set; } = "";
        public string?   CustomerId         { get; set; }
        public string    VendorName         { get; set; } = "";
        public string?   VendorId           { get; set; }
        public string?   InspectionLocation { get; set; }
        public string?   PoNumber           { get; set; }
        public DateTime? InspectionDate     { get; set; }
        public string?   ItemNumber         { get; set; }
        public string?   InspectionType     { get; set; }
        public string    ProductName        { get; set; } = "";
        public string?   ProductType        { get; set; }
        public string?   ProductCategory    { get; set; }
        public int       TotalShipmentQty   { get; set; }
        public int       TotalCartonBoxes   { get; set; }
        public string?   GeneralRemark      { get; set; }
        public string?   InspectorId        { get; set; }
        public string?   InspectorName      { get; set; }
        public string?   InspectionStartTime { get; set; }
        public string?   InspectionEndTime   { get; set; }
        public string?   InspectorRemarks    { get; set; }

        // AQL
        public int    AqlQuantity  { get; set; }
        public string AqlLevel     { get; set; } = "II";
        public string AqlCritical  { get; set; } = "0";
        public string AqlMajor     { get; set; } = "2.5";
        public string AqlMinor     { get; set; } = "4.0";
        // AQL computed results (từ client tính sẵn, cache lại)
        public int AqlCriticalSampleSize { get; set; }
        public int AqlCriticalAccept     { get; set; }
        public int AqlCriticalReject     { get; set; }
        public int AqlMajorSampleSize    { get; set; }
        public int AqlMajorAccept        { get; set; }
        public int AqlMajorReject        { get; set; }
        public int AqlMinorSampleSize    { get; set; }
        public int AqlMinorAccept        { get; set; }
        public int AqlMinorReject        { get; set; }

        public string? Photo1Url { get; set; }
        public string? Photo2Url { get; set; }

        // Sections
        public PackagingRequest?           Packaging        { get; set; }
        public ProductSpecRequest?         ProductSpec      { get; set; }
        public List<PerformanceTestRequest> PerformanceTests { get; set; } = new();
        public List<ReferenceRequest>       References       { get; set; } = new();

        public List<StepRequest>              Steps              { get; set; } = new();
        public List<OverallConclusionRequest> OverallConclusions { get; set; } = new();
    }

    public class CartonRequest
    {
        public int     CartonIndex     { get; set; }
        public string? CartonVariant   { get; set; }
        public string? Description     { get; set; }
        public string? PackingQuantity { get; set; }
        public int?    InnerPackingQty { get; set; }
        public double? InnerL          { get; set; }
        public double? InnerW          { get; set; }
        public double? InnerH          { get; set; }
        public double? InnerWeight     { get; set; }
        public double? OuterL          { get; set; }
        public double? OuterW          { get; set; }
        public double? OuterH          { get; set; }
        public double? OuterWeight     { get; set; }
    }

    public class PackagingRequest
    {
        public int?    BurstingTest        { get; set; }
        public List<CartonRequest> Cartons { get; set; } = new();
        public string? ItemNumber          { get; set; }
        public string? CartonNumber        { get; set; }
        public string? PackagingType       { get; set; }
        public string? CartonColor         { get; set; }
        public string? CardboardType       { get; set; }
        public string? ShippingMark        { get; set; }
        public bool    HasBarcode          { get; set; }
        public int     InnerPackingQty     { get; set; }
        public double  InnerL              { get; set; }
        public double  InnerW              { get; set; }
        public double  InnerH              { get; set; }
        public double  InnerWeight         { get; set; }
        public string? InnerPackingRemark  { get; set; }
        public double  OuterL              { get; set; }
        public double  OuterW              { get; set; }
        public double  OuterH              { get; set; }
        public double  OuterWeight         { get; set; }
        public bool    AssemblyInstruction { get; set; }
        public bool    Hardware            { get; set; }
    }

    public class ProductSpecRequest
    {
        public double   SizeL               { get; set; }
        public double   SizeW               { get; set; }
        public double   SizeH               { get; set; }
        public double   Weight              { get; set; }
        public bool     CompareGoldenSample { get; set; }
        public ColourSwatchRequest[] ColourSwatches { get; set; } = Array.Empty<ColourSwatchRequest>();
    }

    public class ColourSwatchRequest
    {
        public string? Material { get; set; }
        public string? Remark   { get; set; }
    }

    public class PerformanceTestRequest
    {
        public int?    MasterId       { get; set; }
        public string? Category        { get; set; }
        public string? TestItem        { get; set; }
        public string? TestProtocol    { get; set; }
        public string? TestRequirement { get; set; }
        public string? Remark          { get; set; }
    }

    public class ReferenceRequest
    {
        public string? ReferenceName { get; set; }
        public string? FileName      { get; set; }
        public string? FileUrl       { get; set; }
        public string? Remark        { get; set; }
    }

    public class OverallConclusionRequest
    {
        public string  Letter     { get; set; } = "";
        public string  Label      { get; set; } = "";
        public string  Compliance { get; set; } = "";
        public string? Remark     { get; set; }
    }

    public class StepRequest
    {
        public string Title        { get; set; } = "";
        public string? Description { get; set; }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = "";
    }

    public class UpdateStepRequest
    {
        public string  Status { get; set; } = "";
        public string? Note   { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // QC Result DTOs — dùng cho PUT api/inspections/{id}/qc-result
    // ═══════════════════════════════════════════════════════════════

    public class QcResultRequest
    {
        // ── Lưu vào DB column riêng ──────────────────────────
        public string?   FinalResult         { get; set; }   // "PASSED" | "FAILED" | "NA" → Inspection.FinalResult
        public string?   SignatureUrl        { get; set; }   // → Inspection.SignatureUrl
        public DateTime? InspectionDate      { get; set; }   // → Inspection.InspectionDate
        public string?   InspectorComments   { get; set; }   // → Inspection.InspectorComments
        public string?   InspectionLocation  { get; set; }   // → Inspection.InspectionLocation
        public string?   InspectionReference { get; set; }   // → Inspection.QcInspectionRef
        public string?   Photo1Url           { get; set; }   // → Inspection.Photo1Url
        public string?   Photo2Url           { get; set; }   // → Inspection.Photo2Url
        // ── Serialize vào QcResultJson ───────────────────────
        public int       SchemaVersion       { get; set; } = 1;
        public int?      StepSchema          { get; set; }   // 2 = bố cục bước có "Product Weight" riêng (bản nháp cũ không có → dịch lastStep)
        public int?      LastStep            { get; set; }   // bước QC đang làm dở khi Save Draft (để mở lại đúng bước)
        public DateTime? InspectionStartTime { get; set; }   // thời điểm bắt đầu kiểm hàng (màn hình QC). Giờ kết thúc dùng cột Inspection.CompletedAt

        public List<QcOverallConclusionDto>  OverallConclusions { get; set; } = new();
        public QcQuantityConformityDto?      QuantityConformity { get; set; }
        public QcPackagingResultDto?         Packaging          { get; set; }
        public QcProductSpecResultDto?       ProductSpec        { get; set; }
        public QcAqlResultDto?               Aql                { get; set; }
        public List<QcPerformanceTestDto>    PerformanceTests   { get; set; } = new();
        public List<QcDefectDto>             Defects            { get; set; } = new();
    }

    public class QcOverallConclusionDto
    {
        public string  Letter     { get; set; } = "";
        public string  Label      { get; set; } = "";
        public string  Compliance { get; set; } = "";  // "Pass" | "Fail" | "NA"
        public string? Remark     { get; set; }
    }

    public class QcQuantityConformityDto
    {
        public int          ItemPerCarton      { get; set; }
        public int          PresentedPacked    { get; set; }
        public int          PresentedNotPacked { get; set; }
        public int          CartonsPacked      { get; set; }
        public int          CartonsNotPacked   { get; set; }
        public int          QtyNotFinished     { get; set; }
        public List<string> Photos             { get; set; } = new();
    }

    public class QcPackagingResultDto
    {
        public string?                 CartonSizeResult   { get; set; }
        public string?                 ShippingMarkResult { get; set; }
        public string?                 CartonWeightResult { get; set; }
        public string?                 PkgLabelResult     { get; set; }
        public string?                 AssemblyResult     { get; set; }
        public string?                 HardwareResult     { get; set; }
        public List<object>?           CartonSizes        { get; set; }
        public List<object>?           CartonWeights      { get; set; }
        // Legacy photo lists (single carton)
        public List<string>?           ShippingMarkPhotos { get; set; }
        public List<string>?           PkgLabelPhotos     { get; set; }
        public List<string>?           AssemblyPhotos     { get; set; }
        public List<string>?           HardwarePhotos     { get; set; }
        // Mới: data theo từng carton (B-i đến B-vi × CartonNumber)
        public List<QcCartonDataDto>?  CartonDataList     { get; set; }
    }

    public class QcCartonDataDto
    {
        public int          CartonIndex        { get; set; }
        public double       SizeL              { get; set; }
        public double       SizeW              { get; set; }
        public double       SizeH              { get; set; }
        public string?      CartonSizeResult   { get; set; }
        public List<string> SizePhotos         { get; set; } = new();
        public string?      ShippingMarkResult { get; set; }
        public List<string> ShippingMarkPhotos { get; set; } = new();
        public string?      CartonWeightResult { get; set; }
        public double       Weight             { get; set; }
        public List<string> WeightPhotos       { get; set; } = new();   // B-iii Carton Weight photos
        public string?      PkgLabelResult     { get; set; }
        public List<string> PkgLabelPhotos     { get; set; } = new();
        public string?      AssemblyResult     { get; set; }
        public List<string> AssemblyPhotos     { get; set; } = new();
        // B-v chi tiết hướng dẫn lắp ráp. Included = "Yes"|"N/A" (N/A → các ô còn lại là N/A)
        public string?      AssemblyIncluded      { get; set; }
        public string?      AssemblyType          { get; set; }   // Paper Instruction|Instruction Booklet|Instruction Card|QR Code/Digital Instruction
        public string?      AssemblyLanguage      { get; set; }
        public string?      AssemblyPages         { get; set; }
        public string?      AssemblyPrintQuality  { get; set; }   // Good|Acceptable|Poor|N/A
        public string?      AssemblyRemark        { get; set; }
        public string?      HardwareResult     { get; set; }   // Yes|N/A
        public string?      HardwareQuantity   { get; set; }
        public string?      HardwarePackaging  { get; set; }
        public string?      HardwareQuality    { get; set; }   // Good|Acceptable|Poor|N/A
        public string?      HardwareRemark     { get; set; }
        public List<string> HardwarePhotos     { get; set; } = new();
        // B-vii Hangtag. HangtagIncluded = "Yes"|"N/A" (N/A → các ô còn lại là N/A)
        public string?      HangtagIncluded    { get; set; }
        public string?      HangtagType        { get; set; }
        public string?      HangtagShape       { get; set; }
        public string?      HangtagSizeL       { get; set; }
        public string?      HangtagSizeW       { get; set; }
        public string?      HangtagMaterial    { get; set; }
        public string?      HangtagPrintQuality { get; set; }
        public string?      HangtagLanguage    { get; set; }
        public string?      HangtagQuantity    { get; set; }
        public string?      HangtagRemark      { get; set; }
        public List<string> HangtagPhotos      { get; set; } = new();
    }

    public class QcProductSpecResultDto
    {
        public string?         SizeRemark      { get; set; }
        public string?         SizeResult      { get; set; }
        public string?         GoldenRemark    { get; set; }
        public string?         GoldenResult    { get; set; }
        public string?         AdditionalText  { get; set; }
        public List<string>?   SizePhotos      { get; set; }   // ảnh C-i Product Size
        public List<object>?   SizeRows        { get; set; }   // [{photoUrl, l, w, h, note}]
        public List<string>?   GoldenPhotos    { get; set; }
        public List<string>?   SwatchPhotos    { get; set; }
        // C-i kích thước thực đo (so với ProductSpec.SizeL/W/H của hồ sơ sản phẩm)
        public double?         SpecL                { get; set; }
        public double?         SpecW                { get; set; }
        public double?         SpecH                { get; set; }
        // C-ii Product Weight: cân thực tế so với ProductSpec.Weight của hồ sơ sản phẩm
        public double?         ProductWeight        { get; set; }
        public string?         ProductWeightResult  { get; set; }
        public List<string>?   ProductWeightPhotos  { get; set; }
        public int?            GoldenQty            { get; set; }   // số mẫu so với golden sample
        public List<QcColourSwatchDto> ColourSwatches { get; set; } = new();
    }

    public class QcColourSwatchDto
    {
        public int     No       { get; set; }
        public string? Material { get; set; }
        public string? Remark   { get; set; }
        public string? Result   { get; set; }  // "Passed"|"Failed"|"NA"
    }

    public class QcAqlResultDto
    {
        public int    Quantity           { get; set; }
        public string InspectionLevel    { get; set; } = "II";
        public string CriticalAql        { get; set; } = "Not Allowed";
        public string MajorAql           { get; set; } = "2.5";
        public string MinorAql           { get; set; } = "4.0";
        public int    CriticalSampleSize { get; set; }
        public int    MajorSampleSize    { get; set; }
        public int    MinorSampleSize    { get; set; }
        public int    CriticalAccept     { get; set; }
        public int    MajorAccept        { get; set; }
        public int    MinorAccept        { get; set; }
        public int    CriticalFound      { get; set; }
        public int    MajorFound         { get; set; }
        public int    MinorFound         { get; set; }
        public string? Result            { get; set; }  // "Pass"|"Fail"
    }

    public class QcPerformanceTestDto
    {
        public string?      TestItem        { get; set; }
        public string?      TestRequirement { get; set; } // nhận từ client
        public string?      Category        { get; set; }
        public string?      Remark          { get; set; }
        public string?      Result          { get; set; }  // "Passed"|"Failed"|"NA"
        public int          TestQuantity    { get; set; }
        public List<string> Photos          { get; set; } = new();
    }

    public class QcDefectDto
    {
        public string       Type     { get; set; } = "";  // "Critical"|"Major"|"Minor"
        public string?      DefectCode { get; set; }      // F01..F12 (xem DefectCatalog)
        public string?      PhotoUrl { get; set; }        // legacy — giữ tương thích
        public List<string> Photos   { get; set; } = new(); // danh sách ảnh
        public List<string> Remarks  { get; set; } = new(); // remark riêng cho từng ảnh
        public string?      Remark   { get; set; }        // legacy — giữ tương thích
    }
}
