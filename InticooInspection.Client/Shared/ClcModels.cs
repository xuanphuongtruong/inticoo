namespace InticooInspection.Client.Shared
{
    /// <summary>Container Loading Check — model phía client (khớp ClcController.ClcDto, JSON camelCase).</summary>
    public class ClcModel
    {
        public int     Id        { get; set; }
        public string  JobNumber { get; set; } = "";
        public string  Status    { get; set; } = "Draft";

        public string? CustomerCode { get; set; }  public string CustomerName { get; set; } = "";
        public string? CustomerContact { get; set; } public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }  public string? CustomerAddress { get; set; }
        public string? VendorCode { get; set; }    public string VendorName { get; set; } = "";
        public string? VendorContact { get; set; }  public string? VendorPhone { get; set; }
        public string? VendorEmail { get; set; }    public string? VendorAddress { get; set; }

        public string ContainerNo { get; set; } = ""; public string ContainerType { get; set; } = "";
        public string PoNumber { get; set; } = "";    public string? ShipmentReference { get; set; }
        public string? Destination { get; set; }      public DateTime? PlannedLoadingDate { get; set; }
        public string? PlannedStartTime { get; set; } public string? PlannedEndTime { get; set; }
        public string? LoadingMethod { get; set; }    public string? TruckNo { get; set; }
        public string? LoadingLocation { get; set; }  public string? LoadingAddress { get; set; }
        public string? CargoDescription { get; set; }

        public string? InspectorId { get; set; }      public string? InspectorName { get; set; }
        public DateTime? InspectionDate { get; set; }
        public string? InspectionStartTime { get; set; } public string? InspectionEndTime { get; set; }
        public string? JobRemarks { get; set; }

        public string? SevenPointResult { get; set; } public string? AdditionalResult { get; set; }
        public string? LoadingResult { get; set; }    public string? SealResult { get; set; }
        public string? OverallResult { get; set; }
        public bool OnHold { get; set; }

        public bool? CheckClean { get; set; }         public bool? CheckNoMoisture { get; set; }
        public bool? CheckNoOdor { get; set; }        public bool? CheckSuitable { get; set; }
        public bool? CheckDoorSealable { get; set; }  public string? AdditionalRemarks { get; set; }

        public string? SealNumber { get; set; }       public string? SealCondition { get; set; }
        public string? SealAppliedBy { get; set; }    public string? SealPhotoUrl { get; set; }
        public string? SealRemarks { get; set; }

        public DateTime? LoadingDate { get; set; }
        public string? LoadingStartTime { get; set; } public string? LoadingEndTime { get; set; }
        public string? LoadingMethodActual { get; set; }
        public string? CartonCondition { get; set; }  public string? MarkingLabel { get; set; }
        public string? PackagingProtection { get; set; } public string? PalletCondition { get; set; }
        public string? LoadingRemarks { get; set; }   public string? FinalRemarks { get; set; }

        public string? InspectorSignatureUrl { get; set; } public string? InspectorSignName { get; set; }
        public string? InspectorSignPosition { get; set; } public DateTime? InspectorSignedAt { get; set; }
        public string? ReviewerSignatureUrl { get; set; }  public string? ReviewerSignName { get; set; }
        public string? ReviewerSignPosition { get; set; }  public DateTime? ReviewerSignedAt { get; set; }

        public DateTime? CreatedAt { get; set; }      public DateTime? SubmittedAt { get; set; }

        public List<ClcProductModel> Products { get; set; } = new();
        public List<ClcPointModel>   Points   { get; set; } = new();
        public List<ClcPhotoModel>   Photos   { get; set; } = new();

        public ClcPointModel Point(int no)
        {
            var p = Points.FirstOrDefault(x => x.PointNo == no);
            if (p == null) { p = new ClcPointModel { PointNo = no }; Points.Add(p); }
            return p;
        }

        public ClcPhotoModel? Process(int slot) => Photos.FirstOrDefault(p => p.Category == "Process" && p.Slot == slot);
        public IEnumerable<ClcPhotoModel> Additional => Photos.Where(p => p.Category == "Additional");

        public int TotalPlannedCartons => Products.Sum(p => p.PlannedCartons);
        public int TotalPlannedQty     => Products.Sum(p => p.PlannedQty);
        public int TotalActualCartons  => Products.Sum(p => p.ActualCartons ?? 0);
        public int TotalActualQty      => Products.Sum(p => p.ActualQty ?? 0);

        /// <summary>Cùng quy tắc với ClcController.GroupResult.</summary>
        public static string? Group(IEnumerable<string?> results, int expected)
        {
            var graded = results.Where(r => !string.IsNullOrEmpty(r)).ToList();
            if (graded.Any(r => r == "Fail")) return "Fail";
            if (graded.Count == 0) return null;
            if (expected > 0 && graded.Count < expected) return null;
            return graded.All(r => r == "N/A") ? "N/A" : "Pass";
        }

        public void Recompute()
        {
            SevenPointResult = Group(Enumerable.Range(1, 7).Select(i => Points.FirstOrDefault(p => p.PointNo == i)?.Result), 7);
            var adds = new[] { CheckClean, CheckNoMoisture, CheckNoOdor, CheckSuitable, CheckDoorSealable };
            AdditionalResult = adds.Any(a => a == false) ? "Fail" : adds.All(a => a == true) ? "Pass" : null;
            LoadingResult = Group(Products.Select(p => p.Result).Concat(new[] { CartonCondition, MarkingLabel, PackagingProtection, PalletCondition }), 0);
            SealResult = SealCondition == "Damaged" ? "Fail" : SealCondition == "Intact" ? "Pass" : null;
            var parts = new[] { SevenPointResult, AdditionalResult, LoadingResult, SealResult };
            OverallResult = parts.Any(p => p == "Fail") ? "FAIL" : OnHold ? "HOLD" : parts.All(p => p != null) ? "PASS" : null;
        }
    }

    public class ClcProductModel
    {
        public string ProductCode { get; set; } = ""; public string? Description { get; set; }
        public int PlannedCartons { get; set; }       public int PlannedQty { get; set; }
        public string? Unit { get; set; } = "pcs";
        public int? ActualCartons { get; set; }       public int? ActualQty { get; set; }
        public string? Result { get; set; }
    }

    public class ClcPointModel
    {
        public int PointNo { get; set; }  public string? Result { get; set; }
        public string? Findings { get; set; } public string? Remarks { get; set; }
        public List<string> Photos { get; set; } = new();

        public bool HasFinding(string f) => (Findings ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(f);
        public void ToggleFinding(string f, bool on)
        {
            var l = (Findings ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (on && !l.Contains(f)) l.Add(f);
            if (!on) l.Remove(f);
            Findings = string.Join(",", l);
        }
    }

    public class ClcPhotoModel
    {
        public string Category { get; set; } = "Process"; public int Slot { get; set; }
        public string Url { get; set; } = "";             public string? Caption { get; set; }
    }

    public class ClcListItem
    {
        public int Id { get; set; }  public string JobNumber { get; set; } = "";  public string Status { get; set; } = "";
        public string CustomerName { get; set; } = "";  public string VendorName { get; set; } = "";
        public string ContainerNo { get; set; } = "";   public string ContainerType { get; set; } = "";
        public string PoNumber { get; set; } = "";      public DateTime? PlannedLoadingDate { get; set; }
        public string? InspectorName { get; set; }      public DateTime? InspectionDate { get; set; }
        public DateTime? CreatedAt { get; set; }  public string? OverallResult { get; set; }      public int TotalCartons { get; set; }  public int TotalQty { get; set; }
    }

    public static class ClcOptions
    {
        public static readonly string[] ContainerTypes = { "20'GP", "40'GP", "40'HQ", "45'HQ" };
        public static readonly string[] LoadingMethods = { "Pallet + Floor Loading", "Floor Loading", "Pallet Loading", "Slip Sheet" };
        public static readonly string[] LoadingLocations = { "Factory", "Warehouse", "Port / CY", "Other" };
        public static readonly string[] SealConditions = { "Intact", "Damaged" };
        public static readonly string[] SealAppliedBy = { "Factory", "Inspector", "Forwarder", "Customs" };
        public static readonly string[] Findings = { "Structural damage", "Holes or modifications", "Signs of tampering", "Other" };
    }
}
