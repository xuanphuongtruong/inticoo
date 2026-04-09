// InticooInspection.Domain/Entities/Customer.cs
namespace InticooInspection.Domain.Entities
{
    public class Customer
    {
        public int     Id           { get; set; }
        public string  CustomerId   { get; set; } = "";       // e.g. CP100001

        // ── Customer Profile ─────────────────────────────────────────
        public string  CompanyName  { get; set; } = "";
        public string? ShortName    { get; set; }             // SHORT NAME
        public string? BusinessType { get; set; }             // IMPORTER / WHOLESALERS / RETAILERS / TRADING COMPANY / OTHER
        public string? Category     { get; set; }             // PRODUCT CATEGORY
        public string? TaxCode      { get; set; }             // TAX CODE
        public string? BusinessRefNo{ get; set; }             // BUSINESS REF. NO
        public string? Phone        { get; set; }             // PHONE (main phone)
        public string? Website      { get; set; }             // WEBSITE

        // ── Customer Address ─────────────────────────────────────────
        public string? Address1     { get; set; }             // ADDRESS 1
        public string? Address2     { get; set; }             // ADDRESS 2
        public string? City         { get; set; }             // CITY
        public string? State        { get; set; }             // STATE / PROVINCE
        public string? Country      { get; set; }             // COUNTRY
        public string? PostalCode   { get; set; }             // POSTAL / ZIP

        // ── Contact Information ──────────────────────────────────────
        public string? ContactPerson{ get; set; }             // CONTACT NAME
        public string? Position     { get; set; }             // TITLE / POSITION
        public string? Mobile       { get; set; }             // MOBILE
        public string? Email        { get; set; }             // EMAIL

        // ── Remark & Reference ───────────────────────────────────────
        public string? Notes        { get; set; }             // REMARK

        // ── Legacy / kept for compatibility ─────────────────────────
        public string? OfficePhone  { get; set; }             // giữ lại, map từ Phone cũ
        public string? Address      { get; set; }             // giữ lại, map từ Address1

        public bool    IsActive     { get; set; } = true;
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    }
}
