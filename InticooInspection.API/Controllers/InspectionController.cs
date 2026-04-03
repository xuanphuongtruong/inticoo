using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/inspections")]
    [Authorize]
    public class InspectionController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public InspectionController(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET api/inspections
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                // ── Dùng raw SQL-safe projection, không động đến navigation props ──
                var allRaw = await _db.Inspections
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
                        i.InspectorId,
                        i.InspectorName,
                    })
                    .ToListAsync();

                // ── Filter in-memory ──
                if (!string.IsNullOrWhiteSpace(status))
                {
                    var sv = status.ToLower() switch
                    {
                        "ongoing" => (int?)0,
                        "completed" => (int?)1,
                        "pending" => (int?)2,
                        "cancelled" => (int?)3,
                        _ => null
                    };
                    if (sv.HasValue)
                        allRaw = allRaw.Where(i => (int)i.StatusVal == sv.Value).ToList();
                }

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
                    return Ok(new { total, page, pageSize, items = new List<object>() });

                // ── Lookups: load toàn bộ vào memory, tránh Contains() gây lỗi SQL Server cũ ──

                // Inspector lookup — load tất cả users có InspectorId
                var allInspectors = await _userManager.Users
                    .Where(u => u.InspectorId != null && u.InspectorId != "")
                    .Select(u => new { u.InspectorId, u.FullName })
                    .ToListAsync();
                var inspectorByCode = new Dictionary<string, string>();
                foreach (var u in allInspectors)
                    if (u.InspectorId != null && !inspectorByCode.ContainsKey(u.InspectorId))
                        inspectorByCode[u.InspectorId] = u.FullName ?? "";

                // Vendor lookup — load tất cả vendors
                var allVendors = await _db.Vendors
                    .Select(v => new { v.Code, v.CompanyAddress, v.Country })
                    .ToListAsync();
                var vendorAddressDict = allVendors.ToDictionary(v => v.Code, v => v.CompanyAddress ?? "");
                var vendorCountryDict = allVendors.ToDictionary(v => v.Code, v => v.Country ?? "");

                // Product type lookup — load tất cả products
                var allProducts = await _db.Products
                    .Select(p => new { p.ProductName, p.ProductType })
                    .ToListAsync();
                var productTypeDict = new Dictionary<string, string>();
                foreach (var p in allProducts)
                    if (!productTypeDict.ContainsKey(p.ProductName))
                        productTypeDict[p.ProductName] = p.ProductType ?? "";

                // ── Map enums → strings in-memory ──
                // 0=New | 1=OnGoing | 2=Completed | 3=Pending | 4=Cancel
                static string MapStatus(InspectionStatus st) => st switch
                {
                    InspectionStatus.Pending => "New",
                    InspectionStatus.InProgress => "OnGoing",
                    InspectionStatus.Completed => "Completed",
                    InspectionStatus.Cancelled => "Cancel",
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
                    var inspName = inspectorByCode.TryGetValue(iid, out var n) ? n : i.InspectorName ?? "";
                    vendorAddressDict.TryGetValue(i.VendorId ?? "", out var vAddr);
                    vendorCountryDict.TryGetValue(i.VendorId ?? "", out var vCountry);
                    productTypeDict.TryGetValue(i.ProductName ?? "", out var ptype);

                    return (object)new
                    {
                        id = i.Id,
                        title = i.Title,
                        status = MapStatus(i.StatusVal),
                        createdAt = i.CreatedAt,
                        completedAt = i.CompletedAt,
                        jobNumber = i.JobNumber,
                        inspectionDate = i.InspectionDate,
                        customerName = i.CustomerName,
                        customerId = i.CustomerId,
                        vendorName = i.VendorName,
                        vendorId = i.VendorId,
                        vendorAddress = vAddr ?? "",
                        vendorCountry = vCountry ?? "",
                        productCategory = i.ProductCategory,
                        productType = ptype ?? "",
                        inspectionType = MapInspType(i.InspectionTypeVal),
                        inspectorId = iid,
                        inspectorName = inspName
                    };
                }).ToList();

                return Ok(new { total, page, pageSize, items });
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
            var inspection = await _db.Inspections
                .Include(i => i.CreatedBy)
                .Include(i => i.OverallConclusions.OrderBy(o => o.Order))
                .Include(i => i.Packaging)
                .Include(i => i.ProductSpec)
                .Include(i => i.ColourSwatches.OrderBy(c => c.Order))
                .Include(i => i.PerformanceTests.OrderBy(p => p.Order))
                .Include(i => i.References.OrderBy(r => r.Order))
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inspection == null) return NotFound();

            // Lookup ProductCode — gộp vào 1 query, không cần round-trip thứ 2
            var productCode = string.IsNullOrEmpty(inspection.ProductName) ? "" :
                (await _db.Products.AsNoTracking()
                    .Where(p => p.ProductName == inspection.ProductName)
                    .Select(p => new { p.ProductCode })
                    .FirstOrDefaultAsync())?.ProductCode ?? "";

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

            // MapStatus: nhất quán với GetAll (0=New,1=OnGoing,2=Completed,3=Pending,4=Cancel)
            static string MapStatusForEdit(InspectionStatus st) => st switch
            {
                InspectionStatus.Pending => "New",
                InspectionStatus.InProgress => "OnGoing",
                InspectionStatus.Completed => "Completed",
                InspectionStatus.Cancelled => "Cancel",
                _ => "New"
            };

            // Map AQL enums → string
            static string MapDefectAql(DefectAqlLevel? v) => v switch
            {
                DefectAqlLevel.AQL_0_065 => "0.065",
                DefectAqlLevel.AQL_0_1 => "0.10",
                DefectAqlLevel.AQL_0_15 => "0.15",
                DefectAqlLevel.AQL_0_25 => "0.25",
                DefectAqlLevel.AQL_0_4 => "0.40",
                DefectAqlLevel.AQL_0_65 => "0.65",
                DefectAqlLevel.AQL_1_0 => "1.0",
                DefectAqlLevel.AQL_1_5 => "1.5",
                DefectAqlLevel.AQL_2_5 => "2.5",
                DefectAqlLevel.AQL_4_0 => "4.0",
                DefectAqlLevel.AQL_6_5 => "6.5",
                _ => "0"
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

            return Ok(new
            {
                id = inspection.Id,
                title = inspection.Title,
                description = inspection.Description,
                status = MapStatusForEdit(inspection.Status),
                jobNumber = inspection.JobNumber,
                customerName = inspection.CustomerName,
                customerId = inspection.CustomerId,
                vendorName = inspection.VendorName,
                vendorId = inspection.VendorId,
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
                inspectorId = inspection.InspectorId,
                inspectorName = inspection.InspectorName,
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
                photo1Url           = inspection.Photo1Url,
                photo2Url           = inspection.Photo2Url,
                finalResult         = inspection.FinalResult,
                inspectorComments   = inspection.InspectorComments,
                signatureUrl        = inspection.SignatureUrl,
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
                    innerPackingRemark = inspection.Packaging.InnerPackingRemark,
                    outerL = inspection.Packaging.OuterSizeL,
                    outerW = inspection.Packaging.OuterSizeW,
                    outerH = inspection.Packaging.OuterSizeH,
                    outerWeight = inspection.Packaging.OuterWeight,
                    assemblyInstruction = inspection.Packaging.AssemblyInstruction,
                    hardware = inspection.Packaging.Hardware
                },

                // Product Spec
                productSpec = inspection.ProductSpec == null ? null : new
                {
                    sizeL = inspection.ProductSpec.SizeL,
                    sizeW = inspection.ProductSpec.SizeW,
                    sizeH = inspection.ProductSpec.SizeH,
                    weight = inspection.ProductSpec.Weight,
                    compareGoldenSample = inspection.ProductSpec.CompareGoldenSample
                },

                // Colour Swatches
                colourSwatches = inspection.ColourSwatches.Select(c => c.Material).ToList(),

                // Performance Tests
                performanceTests = inspection.PerformanceTests.Select(t => new
                {
                    category = t.Category,
                    testItem = t.TestItem,
                    testRequirement = t.TestRequirement,
                    remark = t.Remark
                }),

                // References
                references = inspection.References.Select(r => new
                {
                    referenceName = r.ReferenceName,
                    fileName = r.FileName,
                    fileUrl = r.FileUrl,
                    remark = r.Remark
                }),

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

        // POST api/inspections
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] CreateInspectionRequest request)
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
                _     => "DPI"
            };
            var yearSuffix = (DateTime.UtcNow.Year % 100).ToString("D2"); // "26", "27", ...
            var jnPrefix   = $"{typePrefix}-JN";

            // Đếm số inspection cùng loại để lấy số thứ tự tiếp theo
            var count = await _db.Inspections
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
                InspectorId = request.InspectorId,
                InspectorName = request.InspectorName,
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
                    InnerPackingRemark = request.Packaging.InnerPackingRemark,
                    OuterSizeL = request.Packaging.OuterL,
                    OuterSizeW = request.Packaging.OuterW,
                    OuterSizeH = request.Packaging.OuterH,
                    OuterWeight = request.Packaging.OuterWeight,
                    AssemblyInstruction = request.Packaging.AssemblyInstruction,
                    Hardware = request.Packaging.Hardware
                },

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
                ColourSwatches = (request.ProductSpec?.ColourSwatches ?? Array.Empty<string>())
                    .Select((m, i) => new InspectionColourSwatch { Order = i + 1, Material = m })
                    .ToList(),

                // D. Performance Testing
                PerformanceTests = (request.PerformanceTests ?? new())
                    .Select((t, i) => new InspectionPerformanceTest
                    {
                        Order = i + 1,
                        Category = t.Category,
                        TestItem = t.TestItem,
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

                Status = InspectionStatus.Pending,
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

        // PUT api/inspections/{id}/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        {
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
        public async Task<IActionResult> UpdateStep(int id, int stepId, [FromBody] UpdateStepRequest request)
        {
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
            var inspection = await _db.Inspections
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
        public async Task<IActionResult> UpdateOverall(int id, [FromBody] List<OverallConclusionRequest> request)
        {
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

        // GET api/inspections/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var total = await _db.Inspections.CountAsync();
                var pending = await _db.Inspections.CountAsync(i => i.Status == InspectionStatus.Pending);
                var inProgress = await _db.Inspections.CountAsync(i => i.Status == InspectionStatus.InProgress);
                var completed = await _db.Inspections.CountAsync(i => i.Status == InspectionStatus.Completed);
                var rate = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0;

                // Load recent inspections — avoid Steps.Count in EF Select projection
                var recentRaw = await _db.Inspections
                    .AsNoTracking()
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(5)
                    .Select(i => new
                    {
                        i.Id,
                        i.Title,
                        i.Status,
                        i.CreatedAt,
                        i.CreatedById,
                    })
                    .ToListAsync();

                var recent = recentRaw.Select(i => (object)new
                {
                    id = i.Id,
                    title = i.Title,
                    status = i.Status.ToString(),
                    createdAt = i.CreatedAt,
                    totalSteps = 0,
                    completedSteps = 0
                }).ToList();

                return Ok(new
                {
                    totalInspections = total,
                    pendingInspections = pending,
                    inProgressInspections = inProgress,
                    completedInspections = completed,
                    completionRate = rate,
                    recentInspections = recent
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
            try
            {
                var inspection = await _db.Inspections
                    .Include(i => i.Steps)
                    .Include(i => i.OverallConclusions)
                    .Include(i => i.Packaging)
                    .Include(i => i.ProductSpec)
                    .Include(i => i.ColourSwatches)
                    .Include(i => i.PerformanceTests)
                    .Include(i => i.References)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (inspection == null) return NotFound();

                // Chỉ cho phép edit khi: New(Pending=0), OnGoing(InProgress=1), Pending(Completed=2 theo enum cũ)
                var editableStatuses = new[]
                {
                InspectionStatus.Pending,
                InspectionStatus.InProgress,
                InspectionStatus.Completed
            };
                if (!editableStatuses.Contains(inspection.Status))
                    return BadRequest(new { error = "Cannot edit an inspection with status Completed or Cancelled." });

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
                inspection.InspectorId = request.InspectorId;
                inspection.InspectorName = request.InspectorName;
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
                    inspection.Packaging.InnerPackingRemark = request.Packaging.InnerPackingRemark;
                    inspection.Packaging.OuterSizeL = request.Packaging.OuterL;
                    inspection.Packaging.OuterSizeW = request.Packaging.OuterW;
                    inspection.Packaging.OuterSizeH = request.Packaging.OuterH;
                    inspection.Packaging.OuterWeight = request.Packaging.OuterWeight;
                    inspection.Packaging.AssemblyInstruction = request.Packaging.AssemblyInstruction;
                    inspection.Packaging.Hardware = request.Packaging.Hardware;
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
                inspection.ColourSwatches = (request.ProductSpec?.ColourSwatches ?? Array.Empty<string>())
                    .Select((m, i) => new InspectionColourSwatch { Order = i + 1, Material = m })
                    .ToList();

                // ── Performance Tests (replace) ──
                _db.RemoveRange(inspection.PerformanceTests);
                inspection.PerformanceTests = (request.PerformanceTests ?? new())
                    .Select((t, i) => new InspectionPerformanceTest
                    {
                        Order = i + 1,
                        Category = t.Category,
                        TestItem = t.TestItem,
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
        [HttpPut("{id}/qc-result")]
        [AllowAnonymous]
        public async Task<IActionResult> SaveQcResult(int id, [FromBody] QcResultRequest request)
        {
            try
            {
                var inspection = await _db.Inspections
                    .Include(i => i.OverallConclusions)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (inspection == null) return NotFound(new { error = "Inspection not found" });

                // ════════════════════════════════════════════════════════
                // 1. Các field quan trọng — lưu thẳng vào DB column riêng
                //    để query/filter/report nhanh, không cần parse JSON
                // ════════════════════════════════════════════════════════
                inspection.FinalResult        = request.FinalResult;       // "PASSED"|"FAILED"|"NA"
                inspection.SignatureUrl       = request.SignatureUrl;
                inspection.InspectorComments  = request.InspectorComments;
                inspection.QcInspectionRef   = request.InspectionReference;
                inspection.InspectionLocation = request.InspectionLocation ?? inspection.InspectionLocation;
                inspection.InspectionDate     = request.InspectionDate ?? inspection.InspectionDate;
                if (!string.IsNullOrEmpty(request.Photo1Url)) inspection.Photo1Url = request.Photo1Url;
                if (!string.IsNullOrEmpty(request.Photo2Url)) inspection.Photo2Url = request.Photo2Url;
                inspection.Status      = InspectionStatus.Completed;
                inspection.CompletedAt = DateTime.UtcNow;

                // ════════════════════════════════════════════════════════
                // 2. Serialize QcResultJson — KHÔNG chứa các field trên
                //    để tránh trùng lặp, chỉ chứa QC data chi tiết
                // ════════════════════════════════════════════════════════
                var qcData = new
                {
                    schemaVersion      = request.SchemaVersion,
                    overallConclusions = request.OverallConclusions,
                    quantityConformity = request.QuantityConformity,
                    packaging          = request.Packaging,
                    productSpec        = request.ProductSpec,
                    aql                = request.Aql,
                    performanceTests   = request.PerformanceTests,
                    defects            = request.Defects,
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
                            Order        = i + 1,
                            Letter       = o.Letter,
                            Label        = o.Label,
                            Compliance   = Enum.TryParse<OverallCompliance>(o.Compliance, true, out var cp)
                                             ? cp : OverallCompliance.None,
                            Remark       = o.Remark
                        }).ToList();
                }

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
    }

    public class CreateInspectionRequest
    {
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
        public string?   ProductCategory    { get; set; }
        public int       TotalShipmentQty   { get; set; }
        public int       TotalCartonBoxes   { get; set; }
        public string?   InspectorId        { get; set; }
        public string?   InspectorName      { get; set; }

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

    public class PackagingRequest
    {
        public string? ItemNumber          { get; set; }
        public string? CartonNumber        { get; set; }
        public string? PackagingType       { get; set; }
        public string? CartonColor         { get; set; }
        public string? CardboardType       { get; set; }
        public string? ShippingMark        { get; set; }
        public bool    HasBarcode          { get; set; }
        public int     InnerPackingQty     { get; set; }
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
        public string[] ColourSwatches      { get; set; } = Array.Empty<string>();
    }

    public class PerformanceTestRequest
    {
        public string? Category        { get; set; }
        public string? TestItem        { get; set; }
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
        public string?      PkgLabelResult     { get; set; }
        public List<string> PkgLabelPhotos     { get; set; } = new();
        public string?      AssemblyResult     { get; set; }
        public List<string> AssemblyPhotos     { get; set; } = new();
        public string?      HardwareResult     { get; set; }
        public List<string> HardwarePhotos     { get; set; } = new();
    }

    public class QcProductSpecResultDto
    {
        public string?         SizeResult      { get; set; }
        public string?         GoldenResult    { get; set; }
        public string?         AdditionalText  { get; set; }
        public List<string>?   SizePhotos      { get; set; }   // ảnh C-i Product Size
        public List<object>?   SizeRows        { get; set; }   // [{photoUrl, l, w, h, note}]
        public List<string>?   GoldenPhotos    { get; set; }
        public List<string>?   SwatchPhotos    { get; set; }
        public List<QcColourSwatchDto> ColourSwatches { get; set; } = new();
    }

    public class QcColourSwatchDto
    {
        public int     No       { get; set; }
        public string? Material { get; set; }
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
    }

    public class QcPerformanceTestDto
    {
        public string?      TestItem     { get; set; }
        public string?      Remark       { get; set; }
        public string?      Result       { get; set; }  // "Passed"|"Failed"|"NA"
        public int          TestQuantity { get; set; }
        public List<string> Photos       { get; set; } = new();
    }

    public class QcDefectDto
    {
        public string       Type     { get; set; } = "";  // "Critical"|"Major"|"Minor"
        public string?      PhotoUrl { get; set; }        // legacy — giữ tương thích
        public List<string> Photos   { get; set; } = new(); // mới — danh sách nhiều ảnh
        public string?      Remark   { get; set; }
    }
}
