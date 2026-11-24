namespace InticooInspection.Client.Shared
{
    // Khớp InticooInspection.API.Controllers.DefectCapaDto (JSON camelCase, đọc không phân biệt hoa thường)

    public class CapaActionModel
    {
        public string?   Action         { get; set; }
        public DateTime? TargetDate     { get; set; }
        public bool      Done           { get; set; }
        public DateTime? CompletionDate { get; set; }
    }

    public class PreventiveActionModel
    {
        public string? Action    { get; set; }
        public string? Frequency { get; set; } = "Monthly";
        public bool    Active    { get; set; } = true;
    }

    public class DefectReferenceModel
    {
        public string? Reference { get; set; }
        public string? FileUrl   { get; set; }
        public string? FileName  { get; set; }
        public string? Remark    { get; set; }
        public bool    Uploading { get; set; }
    }

    /// <summary>1 điểm lỗi trên ảnh (toạ độ % theo kích thước ảnh).</summary>
    public class LocationPointModel
    {
        public double  X     { get; set; }
        public double  Y     { get; set; }
        public string? Label { get; set; }
    }

    public class DefectCapaModel
    {
        public int       Id             { get; set; }
        public string?   DefectNo       { get; set; }
        public string?   CarNo          { get; set; }
        public int?      InspectionId   { get; set; }
        public string?   JobNumber      { get; set; }
        public string?   PoNumber       { get; set; }
        public DateTime? InspectionDate { get; set; }
        public string?   InspectorName  { get; set; }
        public string?   VendorId       { get; set; }
        public string?   VendorName     { get; set; }
        public string?   CustomerId     { get; set; }
        public string?   CustomerName   { get; set; }
        public string?   ProductName    { get; set; }
        public string?   ItemNumber     { get; set; }
        public string?   ProductPhotoUrl { get; set; }

        public string?  DefectCode        { get; set; }
        public string?  DefectCategory    { get; set; }
        public string?  DefectType        { get; set; }
        public string?  Severity          { get; set; } = "Major";
        public int      Quantity          { get; set; }
        public string?  InspectionArea    { get; set; }
        public string?  AqlClassification { get; set; }
        public int?     ProductQtyChecked { get; set; }
        public string?  Description       { get; set; }
        public string?  LocationImageUrl  { get; set; }
        public double?  LocationX         { get; set; }
        public double?  LocationY         { get; set; }
        public string?  LocationLabel     { get; set; }
        public List<LocationPointModel> Locations { get; set; } = new();
        public List<string> Photos        { get; set; } = new();
        public string?  PhotoNotes        { get; set; }
        public List<string> AfterPhotos   { get; set; } = new();
        public string?  RootCauseType     { get; set; }
        public string?  FinalRootCause    { get; set; }
        public string?  Why1 { get; set; }
        public string?  Why2 { get; set; }
        public string?  Why3 { get; set; }
        public string?  Why4 { get; set; }
        public string?  Why5 { get; set; }
        public string?   ResponsibleDepartment { get; set; }
        public string?   ResponsiblePerson     { get; set; }
        public string?   CorrectiveAction      { get; set; }
        public DateTime? TargetDate            { get; set; }
        public string?   Priority              { get; set; } = "Medium";
        public List<CapaActionModel>       CorrectiveActions { get; set; } = new();
        public List<PreventiveActionModel> PreventiveActions { get; set; } = new();
        public string?   CapaRemarks          { get; set; }
        public DateTime? VerificationDate     { get; set; }
        public string?   VerifiedBy           { get; set; }
        public string?   EffectivenessResult  { get; set; }
        public string?   VerificationComments { get; set; }
        public List<DefectReferenceModel> References { get; set; } = new();
        public string?   FactoryRepName       { get; set; }
        public string?   QaVerifierName       { get; set; }
        public string?   CustomerApproverName { get; set; }
        public DateTime? CustomerApprovalDate { get; set; }
        public string?   FactoryRepPosition   { get; set; }
        public string?   QaPosition           { get; set; }
        public string?   CustomerPosition     { get; set; }
        public string?   FactoryRepSignatureUrl { get; set; }
        public DateTime? FactoryRepSignedAt     { get; set; }
        public string?   FactoryRepSignedBy     { get; set; }
        public string?   QaSignatureUrl         { get; set; }
        public DateTime? QaSignedAt             { get; set; }
        public string?   QaSignedBy             { get; set; }
        public string?   CustomerSignatureUrl   { get; set; }
        public DateTime? CustomerSignedAt       { get; set; }
        public string?   CustomerSignedBy       { get; set; }
        public string?   Status      { get; set; }
        public DateTime  OpenDate    { get; set; }
        public DateTime? DueDate     { get; set; }
        public DateTime? CloseDate   { get; set; }
        public bool      AutoCreated { get; set; }

        public int Progress => CorrectiveActions.Count(a => !string.IsNullOrWhiteSpace(a.Action)) is var n && n > 0
            ? (int)Math.Round(100.0 * CorrectiveActions.Count(a => a.Done && !string.IsNullOrWhiteSpace(a.Action)) / n)
            : 0;
    }

    public static class DefectOptions
    {
        public static readonly string[] Categories   = { "Workmanship", "Appearance", "Material", "Hardware / Components", "Function", "Dimension", "Packaging" };
        public static readonly string[] Severities   = { "Critical", "Major", "Minor" };
        public static readonly string[] Areas        = { "Front Leg", "Back Leg", "Seat", "Backrest", "Armrest", "Top", "Frame", "Drawer", "Door", "Packaging", "Other" };
        public static readonly string[] RootCauses   = { "Human Error", "Process Failure", "Material Defect", "Machine Issue", "Packaging Issue", "Supplier Issue", "Design Issue", "Others" };
        public static readonly string[] Departments  = { "Production", "Quality", "Packaging", "Warehouse", "Purchasing", "Engineering", "Management" };
        public static readonly string[] Priorities   = { "Low", "Medium", "High" };
        public static readonly string[] Frequencies  = { "Daily", "Weekly", "Monthly", "Quarterly", "Per Shipment" };
        public static readonly string[] Effectiveness = { "Effective", "Partially Effective", "Not Effective" };

        public static string DefaultCategory(string? code) => code switch
        {
            "F04" => "Appearance", "F06" => "Material", "F07" or "F08" => "Hardware / Components",
            "F10" => "Function", "F11" => "Dimension", "F12" => "Packaging", _ => "Workmanship"
        };

        public static string SeverityCss(string? s) => (s ?? "").ToLower();
        public static string StatusCss(string? s) => s switch { "Closed" => "closed", "In Progress" => "inprogress", _ => "open" };
    }
}
