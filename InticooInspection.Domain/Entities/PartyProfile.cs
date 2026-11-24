// InticooInspection.Domain/Entities/PartyProfile.cs
namespace InticooInspection.Domain.Entities
{
    /// <summary>Người liên hệ của Customer hoặc Vendor (PartyType = "Customer" | "Vendor").</summary>
    public class PartyContact
    {
        public int     Id            { get; set; }
        public string  PartyType     { get; set; } = "Customer";
        public int     PartyId       { get; set; }          // Customer.Id hoặc Vendor.Id
        public string  Name          { get; set; } = "";
        public string? Position      { get; set; }
        public string? Email         { get; set; }
        public string? Phone         { get; set; }
        public bool    ReceiveReports { get; set; } = true;   // nhận inspection report
        public bool    ScheduleEmail  { get; set; }           // nhận email Inspection Schedule (Vendor)
        public bool    IsPrimary     { get; set; }
        public bool    IsActive      { get; set; } = true;
        public int     SortOrder     { get; set; }
    }

    /// <summary>
    /// Cấu hình báo cáo theo Customer/Vendor. Customer có 4 dòng: PPI, DPI, PSI, Schedule.
    /// Vendor chỉ có 1 dòng: Schedule (gồm cả cách gửi report).
    /// </summary>
    public class PartyReportSetting
    {
        public int     Id            { get; set; }
        public string  PartyType     { get; set; } = "Customer";
        public int     PartyId       { get; set; }
        public string  Kind          { get; set; } = "PPI";   // PPI | DPI | PSI | Schedule

        public bool    Enabled       { get; set; } = true;
        // Immediate | ServerOnly | Scheduled (customer) | Manual (vendor)
        public string  DeliveryMode  { get; set; } = "Immediate";

        public string  Language      { get; set; } = "en";
        public string? Subject       { get; set; }
        public string? Body          { get; set; }            // HTML

        // Lịch gửi (Schedule)
        public int     PeriodDays    { get; set; } = 7;        // 7 | 14
        public string? SendDays      { get; set; }            // "Mon,Tue"
        public string? SendTime      { get; set; }            // "09:00"
        public string? TimeZone      { get; set; }
    }
}
