using System.ComponentModel.DataAnnotations;

namespace InticooInspection.Portal.Data;

// Trạng thái dùng chuỗi (lưu DB dạng chữ, dễ đọc khi truy vấn trực tiếp) — hằng số ở đây để không gõ sai.
public static class CompanyStatus
{
    public const string PendingPayment = "PendingPayment";
    public const string PendingReview  = "PendingReview";
    public const string Active         = "Active";
    public const string Overdue        = "Overdue";
    public const string Paused         = "Paused";
    public const string Cancelled      = "Cancelled";
    public static readonly string[] All = { PendingPayment, PendingReview, Active, Overdue, Paused, Cancelled };
}

public static class InvoiceStatus
{
    public const string Pending   = "Pending";
    public const string Paid      = "Paid";
    public const string Overdue   = "Overdue";
    public const string Cancelled = "Cancelled";
    public const string Refunded  = "Refunded";
    public static readonly string[] All = { Pending, Paid, Overdue, Cancelled, Refunded };
}

public static class PaymentStatus
{
    public const string Paid     = "Paid";
    public const string Pending  = "Pending";
    public const string Failed   = "Failed";
    public const string Refunded = "Refunded";
    public static readonly string[] All = { Paid, Pending, Failed, Refunded };
}

public static class SubscriptionStatus
{
    public const string Active         = "Active";
    public const string PendingPayment = "PendingPayment";
    public const string PendingReview  = "PendingReview";
    public const string Overdue        = "Overdue";
    public const string Paused         = "Paused";
    public const string Cancelled      = "Cancelled";
}

public static class ProvisionStatus
{
    public const string None         = "None";          // chưa cấp DB
    public const string Provisioned  = "Provisioned";   // đã tạo DB riêng
    public const string Failed       = "Failed";
}

public class Plan
{
    public int Id { get; set; }
    [MaxLength(30)]  public string Code { get; set; } = "";          // basic / business / professional
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(200)] public string Tagline { get; set; } = "";       // "For small teams"
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int MaxUsers { get; set; }
    public int StorageGb { get; set; }
    public bool IsPopular { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public List<PlanModule> Modules { get; set; } = new();
}

public class PlatformModule
{
    public int Id { get; set; }
    [MaxLength(30)]  public string Code { get; set; } = "";          // inspection / capa / analytics / clc / api
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(300)] public string Description { get; set; } = "";
    [MaxLength(20)]  public string Color { get; set; } = "#2563eb";
    public int SortOrder { get; set; }
}

public class PlanModule
{
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    public int ModuleId { get; set; }
    public PlatformModule? Module { get; set; }
}

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(40)]  public string Code { get; set; } = "";          // slug duy nhất — dùng đặt tên database
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(80)]  public string Country { get; set; } = "";
    [MaxLength(2)]   public string CountryCode { get; set; } = "";
    [MaxLength(300)] public string Address { get; set; } = "";
    [MaxLength(150)] public string ContactPerson { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(40)]  public string Phone { get; set; } = "";
    [MaxLength(200)] public string? Website { get; set; }
    [MaxLength(10)]  public string Language { get; set; } = "en";
    [MaxLength(30)]  public string Status { get; set; } = CompanyStatus.PendingPayment;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsDemo { get; set; }

    // Database riêng của khách (mỗi khách 1 DB + 1 SQL login giới hạn) — chuỗi kết nối được mã hóa AES-GCM
    [MaxLength(100)] public string? DatabaseName { get; set; }
    [MaxLength(2000)] public string? ConnectionStringEnc { get; set; }
    [MaxLength(20)]  public string ProvisionStatus { get; set; } = Data.ProvisionStatus.None;
    [MaxLength(1000)] public string? ProvisionError { get; set; }
    public DateTime? ProvisionedAt { get; set; }

    public double StorageUsedGb { get; set; }
    public DateTime? StorageMeasuredAt { get; set; }

    public List<Subscription> Subscriptions { get; set; } = new();
    public List<CompanyUser> Users { get; set; } = new();
    public List<CompanyModule> Modules { get; set; } = new();
}

public class CompanyModule
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    [MaxLength(30)] public string ModuleCode { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public class Subscription
{
    public int Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public int PlanId { get; set; }
    public Plan? Plan { get; set; }
    [MaxLength(10)] public string BillingCycle { get; set; } = "Monthly";   // Monthly / Annual
    public decimal Price { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? NextBillingDate { get; set; }
    [MaxLength(30)] public string Status { get; set; } = SubscriptionStatus.PendingPayment;
    public bool AutoRenew { get; set; } = true;
    public DateTime? CancelledAt { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = "";             // INV-2026-0001
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    [MaxLength(100)] public string PlanName { get; set; } = "";
    [MaxLength(10)]  public string BillingCycle { get; set; } = "Monthly";
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = InvoiceStatus.Pending;
    public DateTime? PaidAt { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public List<Payment> Payments { get; set; } = new();
}

public class Payment
{
    public int Id { get; set; }
    [MaxLength(60)] public string TransactionId { get; set; } = "";
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(30)] public string Method { get; set; } = "Card";        // Card / BankTransfer / PayPal
    [MaxLength(30)] public string Provider { get; set; } = "";          // Stripe / Test / Manual
    [MaxLength(20)] public string Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(20)] public string? CardBrand { get; set; }
    [MaxLength(4)]  public string? CardLast4 { get; set; }              // KHÔNG bao giờ lưu số thẻ đầy đủ/CVC
    [MaxLength(150)] public string? CardHolder { get; set; }
    [MaxLength(500)] public string? FailureReason { get; set; }
    public List<PaymentEvent> Events { get; set; } = new();
}

public class PaymentEvent
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public Payment? Payment { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

// Bản sao tối thiểu của người dùng thuộc mỗi công ty (dữ liệu gốc nằm trong DB riêng của công ty)
public class CompanyUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    [MaxLength(150)] public string FullName { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(256)] public string? UserName { get; set; }          // tên đăng nhập trong DB khách (API dùng để tìm công ty khi đăng nhập)
    [MaxLength(40)]  public string? Phone { get; set; }
    [MaxLength(80)]  public string Role { get; set; } = "Administrator";
    [MaxLength(80)]  public string? JobTitle { get; set; }
    [MaxLength(80)]  public string? Department { get; set; }
    [MaxLength(10)]  public string Language { get; set; } = "en";
    [MaxLength(20)]  public string Status { get; set; } = "Pending";     // Active / Pending / Inactive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public class SuperAdmin
{
    public int Id { get; set; }
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(150)] public string FullName { get; set; } = "";
    [MaxLength(500)] public string PasswordHash { get; set; } = "";
    [MaxLength(40)]  public string Role { get; set; } = "Super Admin";
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int FailedCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(60)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
}

// Đăng ký chờ thanh toán: giữ mật khẩu admin dưới dạng hash để cấp tài khoản sau khi thanh toán/duyệt
public class Registration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    [MaxLength(150)] public string AdminName { get; set; } = "";
    [MaxLength(200)] public string AdminEmail { get; set; } = "";
    [MaxLength(500)] public string AdminPasswordHash { get; set; } = "";
    [MaxLength(200)] public string Languages { get; set; } = "en,zh,vi,ms";
    [MaxLength(20)]  public string PaymentMethod { get; set; } = "Card";
    public bool EmailVerified { get; set; }
    [MaxLength(80)]  public string? VerifyToken { get; set; }
    public bool Completed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(60)]  public string? IpAddress { get; set; }
}

public class ContactMessage
{
    public int Id { get; set; }
    [MaxLength(150)] public string FullName { get; set; } = "";
    [MaxLength(200)] public string Company { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(80)]  public string Country { get; set; } = "";
    [MaxLength(60)]  public string Subject { get; set; } = "";
    [MaxLength(1000)] public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    [MaxLength(60)] public string? IpAddress { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    [MaxLength(200)] public string Actor { get; set; } = "system";
    [MaxLength(60)]  public string Action { get; set; } = "";
    [MaxLength(60)]  public string Entity { get; set; } = "";
    [MaxLength(80)]  public string? EntityId { get; set; }
    [MaxLength(1000)] public string? Detail { get; set; }
    [MaxLength(60)]  public string? IpAddress { get; set; }
}

public class Setting
{
    [MaxLength(100)] public string Key { get; set; } = "";
    [MaxLength(2000)] public string Value { get; set; } = "";
}
