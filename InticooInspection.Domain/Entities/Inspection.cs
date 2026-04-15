namespace InticooInspection.Domain.Entities
{
    // ═══════════════════════════════════════════════════════
    //  ENUMS
    // ═══════════════════════════════════════════════════════

    public enum InspectionStatus  { Pending = 0, InProgress = 1, Completed = 2, Cancelled = 3 }
    public enum StepStatus        { Pending = 0, Pass = 1, Fail = 2, Skipped = 3 }
    public enum InspectionType    { DPI = 0, PPT = 1, PST = 2 }
    public enum PackagingType     { FSCCarton = 0, NonFSCCarton = 1 }
    public enum CartonColor       { White = 0, Brown = 1 }
    public enum CardboardType     { SingleFace = 0, SingleWall = 1, DoubleWall = 2 }
    public enum ShippingMarkType  { ColorLabel = 0, Printing = 1 }
    public enum DefectAqlLevel
    {
        NotAllowed = 0,
        AQL_0_065 = 1,
        AQL_0_1   = 2,
        AQL_0_15  = 3,
        AQL_0_25  = 4,
        AQL_0_4   = 5,
        AQL_0_65  = 6,
        AQL_1_0   = 7,
        AQL_1_5   = 8,
        AQL_2_5   = 9,
        AQL_4_0   = 10,
        AQL_6_5   = 11,
    }
    public enum AqlInspectionLevel { I = 0, II = 1, III = 2, S1 = 3, S2 = 4, S3 = 5, S4 = 6 }

    // ═══════════════════════════════════════════════════════
    //  MAIN INSPECTION ENTITY
    // ═══════════════════════════════════════════════════════

    public class Inspection
    {
        public int Id { get; set; }

        // ── General Information ──────────────────────────────
        public string  CustomerName       { get; set; } = "";
        public string? CustomerId         { get; set; }
        public string  VendorName         { get; set; } = "";
        public string? VendorId           { get; set; }
        public string? InspectionLocation { get; set; }
        public string? PoNumber           { get; set; }
        public DateTime InspectionDate    { get; set; } = DateTime.UtcNow;
        public string? ItemNumber         { get; set; }
        public InspectionType InspectionType { get; set; } = InspectionType.DPI;
        public string  ProductName        { get; set; } = "";
        public bool    InspectionReference{ get; set; } = true;
        public int     TotalShipmentQty   { get; set; }
        public string? ProductCategory    { get; set; }
        public int     TotalCartonBoxes   { get; set; }
        public string? GeneralRemark      { get; set; }  // Remark for General Information section

        // Photos (lưu path hoặc URL)
        public string? Photo1Url { get; set; }
        public string? Photo2Url { get; set; }

        // ── AQL Sampling Simulator ───────────────────────────
        public AqlInspectionLevel AqlInspectionLevel { get; set; } = AqlInspectionLevel.II;
        public DefectAqlLevel CriticalAql { get; set; } = DefectAqlLevel.NotAllowed;
        public DefectAqlLevel MajorAql    { get; set; } = DefectAqlLevel.AQL_2_5;
        public DefectAqlLevel MinorAql    { get; set; } = DefectAqlLevel.AQL_4_0;

        // AQL computed results (cache để report nhanh)
        public int CriticalSampleSize { get; set; }
        public int CriticalAccept     { get; set; }
        public int CriticalReject     { get; set; }
        public int MajorSampleSize    { get; set; }
        public int MajorAccept        { get; set; }
        public int MajorReject        { get; set; }
        public int MinorSampleSize    { get; set; }
        public int MinorAccept        { get; set; }
        public int MinorReject        { get; set; }

        // ── Status & Audit ───────────────────────────────────
        public InspectionStatus Status    { get; set; } = InspectionStatus.Pending;
        public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt      { get; set; }
        public string CreatedById         { get; set; } = "";
        public AppUser? CreatedBy         { get; set; }

        // ── Office Use ───────────────────────────────────────
        public string?    InspectorName   { get; set; }
        public string?    InspectorId     { get; set; }
        public DateTime?  OfficeDate      { get; set; }
        public string?    JobNumber       { get; set; }  // Auto-generated

        // ── Navigation Properties ────────────────────────────
        public InspectionPackaging?             Packaging    { get; set; }
        public InspectionProductSpec?           ProductSpec  { get; set; }
        public ICollection<InspectionColourSwatch>   ColourSwatches   { get; set; } = new List<InspectionColourSwatch>();
        public ICollection<InspectionPerformanceTest> PerformanceTests { get; set; } = new List<InspectionPerformanceTest>();
        public ICollection<InspectionReference>          References          { get; set; } = new List<InspectionReference>();
        public ICollection<InspectionOverallConclusion>  OverallConclusions  { get; set; } = new List<InspectionOverallConclusion>();

        // Legacy — giữ lại tương thích ngược
        public string? Title       { get; set; }
        public string? Description { get; set; }
        public ICollection<InspectionStep> Steps { get; set; } = new List<InspectionStep>();

        // ── QC Result (lưu sau khi QC inspector thực hiện kiểm hàng) ─────
        public string? QcResultJson      { get; set; }  // toàn bộ QC data JSON (backup)
        public string? InspectorComments { get; set; }
        public string? FinalResult       { get; set; }  // "PASSED" | "FAILED" | "NA"
        public string? SignatureUrl      { get; set; }
        public string? QcInspectionRef  { get; set; }  // Inspection Reference nhập tay khi QC

        // QC navigation properties
        public InspectionQcQuantityConformity?      QcQuantityConformity { get; set; }
        public InspectionQcAqlResult?               QcAqlResult          { get; set; }
        public ICollection<InspectionQcDefect>      QcDefects            { get; set; } = new List<InspectionQcDefect>();
    }

    // ═══════════════════════════════════════════════════════
    //  B. PACKAGING & IDENTIFICATION
    // ═══════════════════════════════════════════════════════

    public class InspectionPackaging
    {
        public int    Id           { get; set; }
        public int    InspectionId { get; set; }
        public Inspection? Inspection { get; set; }

        public string?        ItemNumber          { get; set; }
        public string?        CartonNumber        { get; set; }
        public PackagingType? PackagingType       { get; set; }
        public CartonColor?   CartonColor         { get; set; }
        public CardboardType? CardboardType       { get; set; }
        public ShippingMarkType? ShippingMark     { get; set; }
        public bool           HasBarcode          { get; set; }

        // Inner Packing
        public int?    InnerPackingQty    { get; set; }
        public double? InnerSizeL         { get; set; }   // mm
        public double? InnerSizeW         { get; set; }   // mm
        public double? InnerSizeH         { get; set; }   // mm
        public double? InnerWeight        { get; set; }   // kg
        public string? InnerPackingRemark { get; set; }

        // Outer Packing
        public double? OuterSizeL   { get; set; }   // mm
        public double? OuterSizeW   { get; set; }   // mm
        public double? OuterSizeH   { get; set; }   // mm
        public double? OuterWeight  { get; set; }   // kg

        public bool AssemblyInstruction { get; set; }
        public bool Hardware            { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  C. PRODUCT SPECIFICATION
    // ═══════════════════════════════════════════════════════

    public class InspectionProductSpec
    {
        public int    Id           { get; set; }
        public int    InspectionId { get; set; }
        public Inspection? Inspection { get; set; }

        // i. Product Size
        public double? SizeL   { get; set; }   // mm
        public double? SizeW   { get; set; }   // mm
        public double? SizeH   { get; set; }   // mm
        public double? Weight  { get; set; }   // kg

        // ii. Compare Golden Sample
        public bool CompareGoldenSample { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  C-iii. COLOUR SWATCH COMPARISON
    // ═══════════════════════════════════════════════════════

    public class InspectionColourSwatch
    {
        public int    Id           { get; set; }
        public int    InspectionId { get; set; }
        public Inspection? Inspection { get; set; }

        public int    Order    { get; set; }
        public string Material { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════
    //  D. PERFORMANCE TESTING
    // ═══════════════════════════════════════════════════════

    public class InspectionPerformanceTest
    {
        public int    Id           { get; set; }
        public int    InspectionId { get; set; }
        public Inspection? Inspection { get; set; }

        public int     Order           { get; set; }
        public int?    MasterId        { get; set; }   // FK → PerformanceTestMasters.Id
        public string? Category        { get; set; }
        public string? TestItem        { get; set; }
        public string? TestProtocol    { get; set; }
        public string? TestRequirement { get; set; }
        public string? Remark          { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  E. REFERENCE
    // ═══════════════════════════════════════════════════════

    public class InspectionReference
    {
        public int    Id           { get; set; }
        public int    InspectionId { get; set; }
        public Inspection? Inspection { get; set; }

        public int     Order         { get; set; }
        public string? ReferenceName { get; set; }
        public string? FileUrl       { get; set; }   // path/URL sau khi upload
        public string? FileName      { get; set; }   // tên file gốc
        public string? Remark        { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  OVERALL CONCLUSION
    // ═══════════════════════════════════════════════════════

    public enum OverallCompliance { None = 0, Pass = 1, Fail = 2, NA = 3 }

    public class InspectionOverallConclusion
    {
        public int    Id           { get; set; }
        public int    InspectionId { get; set; }
        public Inspection? Inspection { get; set; }

        public int    Order      { get; set; }   // A=1, B=2, ..., F=6
        public string Letter     { get; set; } = "";  // "A","B",...
        public string Label      { get; set; } = "";
        public OverallCompliance Compliance { get; set; } = OverallCompliance.None;
        public string? Remark    { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  LEGACY — InspectionStep (giữ nguyên)
    // ═══════════════════════════════════════════════════════

    public class InspectionStep
    {
        public int    Id           { get; set; }
        public int    InspectionId { get; set; }
        public int    Order        { get; set; }
        public string Title        { get; set; } = "";
        public string? Description { get; set; }
        public StepStatus Status   { get; set; } = StepStatus.Pending;
        public string? Note        { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Inspection? Inspection { get; set; }
    }

    // ═══════════════════════════════════════════════════════
    //  QC RESULT ENTITIES (lưu kết quả kiểm hàng thực tế)
    // ═══════════════════════════════════════════════════════

    /// <summary>A. Quantity Conformity — số liệu đếm hàng thực tế</summary>
    public class InspectionQcQuantityConformity
    {
        public int    Id                 { get; set; }
        public int    InspectionId       { get; set; }
        public Inspection? Inspection    { get; set; }

        public int    ItemPerCarton      { get; set; }
        public int    PresentedPacked    { get; set; }
        public int    PresentedNotPacked { get; set; }
        public int    CartonsPacked      { get; set; }
        public int    CartonsNotPacked   { get; set; }
        public int    QtyNotFinished     { get; set; }
        public string PhotosJson         { get; set; } = "[]";  // List<string> URLs
    }

    /// <summary>E. AQL kết quả thực tế (số defect tìm được)</summary>
    public class InspectionQcAqlResult
    {
        public int    Id               { get; set; }
        public int    InspectionId     { get; set; }
        public Inspection? Inspection  { get; set; }

        public int    Quantity            { get; set; }
        public string InspectionLevel     { get; set; } = "II";
        public string CriticalAql         { get; set; } = "Not Allowed";
        public string MajorAql            { get; set; } = "2.5";
        public string MinorAql            { get; set; } = "4.0";
        public int    CriticalSampleSize  { get; set; }
        public int    MajorSampleSize     { get; set; }
        public int    MinorSampleSize     { get; set; }
        public int    CriticalAccept      { get; set; }
        public int    MajorAccept         { get; set; }
        public int    MinorAccept         { get; set; }
        public int    CriticalFound       { get; set; }
        public int    MajorFound          { get; set; }
        public int    MinorFound          { get; set; }
    }

    /// <summary>F. Chi tiết từng lỗi tìm được (Critical / Major / Minor)</summary>
    public class InspectionQcDefect
    {
        public int    Id           { get; set; }
        public int    InspectionId { get; set; }
        public Inspection? Inspection { get; set; }

        public int    Order      { get; set; }
        public string DefectType { get; set; } = "";  // "Critical" | "Major" | "Minor"
        public string? PhotoUrl  { get; set; }
        public string? Remark    { get; set; }
    }

}