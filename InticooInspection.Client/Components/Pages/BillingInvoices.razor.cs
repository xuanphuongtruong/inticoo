using Microsoft.JSInterop;

namespace InticooInspection.Client.Components.Pages
{
    public partial class BillingInvoices
    {
    /// <summary>Tải 1 hóa đơn dạng trang HTML (mở bằng trình duyệt rồi in / lưu PDF).</summary>
    private async Task DownloadOne(InvoiceDto v)
    {
        string E(string s) => System.Net.WebUtility.HtmlEncode(s);
        string Row(string k, string val, bool b = false) => $"<tr><td>{E(k)}</td><td style='text-align:right'>{(b ? "<b>" : "")}{E(val)}{(b ? "</b>" : "")}</td></tr>";
        var html = $@"<!doctype html><html><head><meta charset='utf-8'><title>{E(v.Number)}</title>
<style>body{{font-family:Segoe UI,Arial,sans-serif;color:#16233f;max-width:720px;margin:40px auto;padding:0 20px}}h1{{margin:0;color:#0c1d4f}}
table{{width:100%;border-collapse:collapse;margin-top:18px}}td{{padding:8px 0;border-bottom:1px solid #e5eaf3}}.m{{color:#64748b}}</style></head><body>
<div style='display:flex;justify-content:space-between'><div><div class='m'>INTICOO GLOBAL SERVICES</div><h1>{E(T("Settings.Invoice"))} {E(v.Number)}</h1></div><div style='text-align:right'><b>{E(TE("Status", v.Status) ?? v.Status)}</b></div></div>
<p class='m' style='margin-top:20px'>{E(T("Settings.BilledTo"))}</p><p><b>{E(_bill.Company)}</b><br>{E(_bill.Addr1)}<br>{E(_bill.Addr2)}</p>
<table>{Row(T("Settings.InvoiceDate"), D(v.InvoiceDate))}{Row(T("Settings.DueDate"), D(v.DueDate))}{Row(T("Settings.Period"), D(v.PeriodStart) + " – " + D(v.PeriodEnd))}
{Row(T("Settings.Plan"), v.Plan)}{Row(T("Settings.Subtotal"), Money(v.Amount))}{Row(T("Settings.Tax"), Money(v.Tax))}{Row(T("Settings.Total"), Money(v.Total), true)}
{(v.PaidAt != null ? Row(T("Settings.PaidOn"), D(v.PaidAt.Value) + (v.Method != null ? " · " + MethodName(v.Method) : "")) : "")}</table></body></html>";
        await JS.InvokeVoidAsync("ptmDownloadBlob", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html)), "text/html", v.Number + ".html", false, T("Users.ErrorOccurred"));
    }

    }
}
