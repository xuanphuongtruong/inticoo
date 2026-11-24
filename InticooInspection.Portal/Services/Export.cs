using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace InticooInspection.Portal.Services;

public static class Export
{
    public const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Ô bắt đầu bằng = + - @ bị Excel hiểu là công thức → thêm dấu nháy để chặn CSV/Excel injection
    private static string Safe(string? s) => !string.IsNullOrEmpty(s) && "=+-@\t\r".Contains(s[0]) ? "'" + s : s ?? "";

    public static FileContentResult Xlsx(string sheet, string[] headers, IEnumerable<object?[]> rows, string fileName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheet.Length > 30 ? sheet[..30] : sheet);
        for (var c = 0; c < headers.Length; c++) { ws.Cell(1, c + 1).Value = headers[c]; ws.Cell(1, c + 1).Style.Font.Bold = true; ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEFB"); }
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                switch (row[c])
                {
                    case null: break;
                    case decimal m: cell.Value = m; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                    case int i: cell.Value = i; break;
                    case double d: cell.Value = d; break;
                    case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm"; break;
                    default: cell.Value = Safe(row[c]!.ToString()); break;
                }
            }
            r++;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 60);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new FileContentResult(ms.ToArray(), XlsxType) { FileDownloadName = fileName };
    }
}
