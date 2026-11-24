namespace InticooInspection.Domain.Entities
{
    /// <summary>
    /// Defect Management / CAPA — 1 bản ghi = 1 lỗi (cùng mã lỗi + cùng mức độ trong 1 inspection)
    /// cần xử lý khắc phục. Được hệ thống tự mở khi QC bấm Done (vượt ngưỡng), hoặc tạo tay.
    /// Các danh sách (ảnh, hành động, tham chiếu) lưu JSON để form linh hoạt.
    /// </summary>
    public class DefectCapa
    {
        public int     Id            { get; set; }
        public string  DefectNo      { get; set; } = "";   // DEF-001
        public string  CarNo         { get; set; } = "";   // CAR-2026-001

        // ── Inspection / sản phẩm ──
        public int?      InspectionId   { get; set; }
        public string?   JobNumber      { get; set; }
        public string?   PoNumber       { get; set; }
        public DateTime? InspectionDate { get; set; }
        public string?   InspectorName  { get; set; }
        public string?   VendorId       { get; set; }
        public string?   VendorName     { get; set; }      // Factory
        public string?   CustomerId     { get; set; }
        public string?   CustomerName   { get; set; }
        public string?   ProductName    { get; set; }
        public string?   ItemNumber     { get; set; }

        // ── 1. Defect Information ──
        public string?  DefectCode        { get; set; }   // F01..F12
        public string?  DefectCategory    { get; set; }   // Workmanship / Material / ...
        public string?  DefectType        { get; set; }   // Scratch / Surface Damage
        public string   Severity          { get; set; } = "Major";   // Critical | Major | Minor
        public int      Quantity          { get; set; }
        public string?  InspectionArea    { get; set; }
        public string?  AqlClassification { get; set; }
        public int?     ProductQtyChecked { get; set; }

        // ── 2-3. Description & Product location ──
        public string?  Description      { get; set; }
        public string?  LocationImageUrl { get; set; }
        public double?  LocationX        { get; set; }    // % theo chiều ngang ảnh
        public double?  LocationY        { get; set; }    // % theo chiều dọc ảnh
        public string?  LocationLabel    { get; set; }
        // Nhiều vị trí lỗi trên cùng ảnh: [{ "x": %, "y": %, "label": "Front Leg" }, ...]
        // (LocationX/Y/Label ở trên giữ điểm đầu tiên để tương thích dữ liệu cũ)
        public string   LocationsJson    { get; set; } = "[]";

        // ── 4. Photo evidence ──
        public string   PhotosJson       { get; set; } = "[]";   // ảnh lúc kiểm (before)
        public string?  PhotoNotes       { get; set; }
        public string   AfterPhotosJson  { get; set; } = "[]";   // ảnh sau khắc phục (re-inspection)

        // ── 5. Root cause ──
        public string?  RootCauseType    { get; set; }
        public string?  FinalRootCause   { get; set; }
        public string?  Why1 { get; set; }
        public string?  Why2 { get; set; }
        public string?  Why3 { get; set; }
        public string?  Why4 { get; set; }
        public string?  Why5 { get; set; }

        // ── 6. Corrective Action Request ──
        public string?   ResponsibleDepartment { get; set; }
        public string?   ResponsiblePerson     { get; set; }
        public string?   CorrectiveAction      { get; set; }
        public DateTime? TargetDate            { get; set; }
        public string?   Priority              { get; set; }   // Low | Medium | High

        // ── 7. CAPA tracking ──
        public string   CorrectiveActionsJson { get; set; } = "[]";
        public string   PreventiveActionsJson { get; set; } = "[]";
        public string?  CapaRemarks           { get; set; }

        // ── 8. Verification & closure ──
        public DateTime? VerificationDate     { get; set; }
        public string?   VerifiedBy           { get; set; }
        public string?   EffectivenessResult  { get; set; }   // Effective | Partially Effective | Not Effective
        public string?   VerificationComments { get; set; }
        public string    ReferencesJson       { get; set; } = "[]";

        // ── Approval ──
        public string?  FactoryRepName       { get; set; }
        public string?  QaVerifierName       { get; set; }
        public string?  CustomerApproverName { get; set; }
        public DateTime? CustomerApprovalDate { get; set; }
        // Chức vụ người ký (nhập trong khung ký)
        public string?  FactoryRepPosition   { get; set; }
        public string?  QaPosition           { get; set; }
        public string?  CustomerPosition     { get; set; }

        // Chữ ký điện tử — mỗi người tự đăng nhập và ký ở thời điểm khác nhau
        public string?   FactoryRepSignatureUrl { get; set; }
        public DateTime? FactoryRepSignedAt     { get; set; }
        public string?   FactoryRepSignedBy     { get; set; }   // username của tài khoản thực hiện ký
        public string?   QaSignatureUrl         { get; set; }
        public DateTime? QaSignedAt             { get; set; }
        public string?   QaSignedBy             { get; set; }
        public string?   CustomerSignatureUrl   { get; set; }
        public DateTime? CustomerSignedAt       { get; set; }
        public string?   CustomerSignedBy       { get; set; }

        // ── Trạng thái ──
        public string    Status      { get; set; } = "Open";   // Open | In Progress | Closed
        public DateTime  OpenDate    { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate     { get; set; }
        public DateTime? CloseDate   { get; set; }
        public bool      AutoCreated { get; set; }
        public DateTime  CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt   { get; set; }
    }
}
