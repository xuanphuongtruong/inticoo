// InticooInspection.Domain/Entities/ContainerCheck.cs
namespace InticooInspection.Domain.Entities
{
    /// <summary>Trạng thái job Container Loading Check.</summary>
    public static class ClcStatus
    {
        public const string Draft     = "Draft";       // mới tạo / lưu nháp job request
        public const string Pending   = "Pending";     // đã giao inspector, chưa kiểm
        public const string InProgress = "InProgress"; // đang kiểm hiện trường
        public const string Submitted = "Submitted";   // đã gửi duyệt (hoàn tất kiểm)
    }

    /// <summary>Kết quả mỗi hạng mục: Pass | Fail | N/A (rỗng = chưa chấm).</summary>
    public static class ClcResult
    {
        public const string Pass = "Pass";
        public const string Fail = "Fail";
        public const string NA   = "N/A";
    }

    /// <summary>
    /// Container Loading Check (CLC): kiểm container 7 điểm trước khi xếp hàng + xác nhận xếp hàng + seal.
    /// Job Number dạng CLC-yyyyMMdd-NNN.
    /// </summary>
    public class ContainerCheck
    {
        public int     Id        { get; set; }
        public string  JobNumber { get; set; } = "";
        public string  Status    { get; set; } = ClcStatus.Draft;

        // ── Customer ───────────────────────────────────────────
        public string? CustomerCode    { get; set; }          // Customer.CustomerId (CP000001)
        public string  CustomerName    { get; set; } = "";
        public string? CustomerContact { get; set; }
        public string? CustomerPhone   { get; set; }
        public string? CustomerEmail   { get; set; }
        public string? CustomerAddress { get; set; }

        // ── Supplier / Factory ─────────────────────────────────
        public string? VendorCode      { get; set; }          // Vendor.Code
        public string  VendorName      { get; set; } = "";
        public string? VendorContact   { get; set; }
        public string? VendorPhone     { get; set; }
        public string? VendorEmail     { get; set; }
        public string? VendorAddress   { get; set; }

        // ── Container & Shipment ───────────────────────────────
        public string    ContainerNo      { get; set; } = "";
        public string    ContainerType    { get; set; } = "";  // 20'GP, 40'GP, 40'HQ, 45'HQ...
        public string    PoNumber         { get; set; } = "";
        public string?   ShipmentReference{ get; set; }
        public string?   Destination      { get; set; }
        public DateTime? PlannedLoadingDate { get; set; }
        public string?   PlannedStartTime { get; set; }        // "HH:mm"
        public string?   PlannedEndTime   { get; set; }        // "HH:mm"
        public string?   LoadingMethod    { get; set; }
        public string?   TruckNo          { get; set; }
        public string?   LoadingLocation  { get; set; }        // Factory | Warehouse | Port ...
        public string?   LoadingAddress   { get; set; }
        public string?   CargoDescription { get; set; }

        // ── Inspector assignment ───────────────────────────────
        public string?   InspectorId      { get; set; }        // AppUser.Id
        public string?   InspectorName    { get; set; }
        public DateTime? InspectionDate   { get; set; }
        public string?   InspectionStartTime { get; set; }     // "HH:mm"
        public string?   InspectionEndTime   { get; set; }
        public string?   JobRemarks       { get; set; }

        // ── Kết quả theo nhóm (cột riêng để truy vấn/dashboard) ─
        public string? SevenPointResult   { get; set; }        // Pass | Fail | N/A
        public string? AdditionalResult   { get; set; }
        public string? LoadingResult      { get; set; }
        public string? SealResult         { get; set; }
        public string? OverallResult      { get; set; }        // PASS | FAIL | HOLD
        public bool    OnHold             { get; set; }        // inspector danh dau tam giu (HOLD) de xem xet lai

        // ── Additional container checks (5 mục Yes/No) ─────────
        public bool?   CheckClean         { get; set; }        // Container is clean
        public bool?   CheckNoMoisture    { get; set; }        // No moisture or water damage
        public bool?   CheckNoOdor        { get; set; }        // No unusual odor
        public bool?   CheckSuitable      { get; set; }        // Suitable for cargo loading
        public bool?   CheckDoorSealable  { get; set; }        // Container door sealable
        public string? AdditionalRemarks  { get; set; }

        // ── Container seal (sau khi xếp hàng) ──────────────────
        public string? SealNumber         { get; set; }
        public string? SealCondition      { get; set; }        // Intact | Damaged
        public string? SealAppliedBy      { get; set; }        // Factory | Inspector | Forwarder | Customs
        public string? SealPhotoUrl       { get; set; }
        public string? SealRemarks        { get; set; }

        // ── Loading verification ───────────────────────────────
        public DateTime? LoadingDate      { get; set; }
        public string?   LoadingStartTime { get; set; }
        public string?   LoadingEndTime   { get; set; }
        public string?   LoadingMethodActual { get; set; }
        public string?   CartonCondition  { get; set; }        // Pass | Fail | N/A
        public string?   MarkingLabel     { get; set; }
        public string?   PackagingProtection { get; set; }
        public string?   PalletCondition  { get; set; }
        public string?   LoadingRemarks   { get; set; }

        public string?   FinalRemarks     { get; set; }

        // ── Chữ ký ─────────────────────────────────────────────
        public string?   InspectorSignatureUrl { get; set; }
        public string?   InspectorSignName     { get; set; }
        public string?   InspectorSignPosition { get; set; }
        public DateTime? InspectorSignedAt     { get; set; }
        public string?   ReviewerSignatureUrl  { get; set; }
        public string?   ReviewerSignName      { get; set; }
        public string?   ReviewerSignPosition  { get; set; }
        public DateTime? ReviewerSignedAt      { get; set; }

        public DateTime  CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt   { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string?   CreatedById { get; set; }

        public ICollection<ContainerCheckPoint>   Points   { get; set; } = new List<ContainerCheckPoint>();
        public ICollection<ContainerCheckProduct> Products { get; set; } = new List<ContainerCheckProduct>();
        public ICollection<ContainerCheckPhoto>   Photos   { get; set; } = new List<ContainerCheckPhoto>();
    }

    /// <summary>7-point container inspection: 1 Front Wall · 2 Left Side · 3 Right Side · 4 Floor · 5 Ceiling/Roof · 6 Doors · 7 Outside/Undercarriage.</summary>
    public class ContainerCheckPoint
    {
        public int    Id      { get; set; }
        public int    CheckId { get; set; }
        public ContainerCheck? Check { get; set; }

        public int     PointNo   { get; set; }                 // 1..7
        public string? Result    { get; set; }                 // Pass | Fail | N/A
        public string? Findings  { get; set; }                 // csv: Structural damage, Holes or modifications, Signs of tampering, Other
        public string? Remarks   { get; set; }
        public string? PhotosJson { get; set; }                // List<string> URL
    }

    /// <summary>Dòng sản phẩm của job: kế hoạch (carton/qty) và thực tế khi xếp hàng.</summary>
    public class ContainerCheckProduct
    {
        public int    Id      { get; set; }
        public int    CheckId { get; set; }
        public ContainerCheck? Check { get; set; }

        public int     SortOrder     { get; set; }
        public string  ProductCode   { get; set; } = "";
        public string? Description   { get; set; }
        public int     PlannedCartons { get; set; }
        public int     PlannedQty    { get; set; }
        public string? Unit          { get; set; } = "pcs";
        public int?    ActualCartons { get; set; }
        public int?    ActualQty     { get; set; }
        public string? Result        { get; set; }             // Pass | Fail | N/A
    }

    /// <summary>Ảnh xếp hàng: Category = Process (6 ảnh quy trình) | Additional (ảnh bổ sung).</summary>
    public class ContainerCheckPhoto
    {
        public int    Id      { get; set; }
        public int    CheckId { get; set; }
        public ContainerCheck? Check { get; set; }

        public string  Category  { get; set; } = "Process";
        public int     Slot      { get; set; }                 // Process: 1..6
        public string  Url       { get; set; } = "";
        public string? Caption   { get; set; }
    }
}
