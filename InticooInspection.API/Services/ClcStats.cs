using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services
{
    /// <summary>1 job Container Loading Check đã "làm phẳng" cho thống kê Dashboard / Inspector / Vendor.</summary>
    public class ClcStat
    {
        public int      Id           { get; init; }
        public string   JobNumber    { get; init; } = "";
        public string   Status       { get; init; } = "";
        public DateTime Date         { get; init; }          // ngày kiểm (fallback: ngày xếp hàng dự kiến, ngày tạo)
        public string?  CustomerCode { get; init; }
        public string   CustomerName { get; init; } = "";
        public string?  VendorCode   { get; init; }
        public string   VendorName   { get; init; } = "";
        public string?  InspectorUserId { get; init; }       // AppUser.Id
        public string?  Overall      { get; init; }          // PASS | FAIL | HOLD | null

        public bool Submitted => Status == ClcStatus.Submitted;
        public bool Passed    => Submitted && Overall == "PASS";
        public bool Failed    => Submitted && Overall == "FAIL";
        public bool Held      => Submitted && Overall == "HOLD";
        public bool Judged    => Passed || Failed;
        public bool Decided   => Passed || Failed || Held;
    }

    public static class ClcStats
    {
        /// <summary>Nạp CLC (bỏ Draft). Draft chỉ là job request chưa chốt nên không tính vào thống kê.</summary>
        public static async Task<List<ClcStat>> LoadAsync(AppDbContext db, Func<IQueryable<ContainerCheck>, IQueryable<ContainerCheck>>? filter = null)
        {
            var q = db.ContainerChecks.AsNoTracking().Where(c => c.Status != ClcStatus.Draft);
            if (filter != null) q = filter(q);
            var rows = await q.Select(c => new
            {
                c.Id, c.JobNumber, c.Status, c.InspectionDate, c.PlannedLoadingDate, c.CreatedAt,
                c.CustomerCode, c.CustomerName, c.VendorCode, c.VendorName, c.InspectorId, c.OverallResult
            }).ToListAsync();

            return rows.Select(r => new ClcStat
            {
                Id = r.Id, JobNumber = r.JobNumber, Status = r.Status,
                Date = r.InspectionDate ?? r.PlannedLoadingDate ?? r.CreatedAt,
                CustomerCode = r.CustomerCode, CustomerName = r.CustomerName,
                VendorCode = r.VendorCode, VendorName = r.VendorName,
                InspectorUserId = r.InspectorId, Overall = r.OverallResult,
            }).ToList();
        }

        public static int InMonth(IEnumerable<ClcStat> l, int month) => l.Count(i => i.Date.Month == month);
    }
}
