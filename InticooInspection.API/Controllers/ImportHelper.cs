using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;

namespace InticooInspection.API.Controllers
{
    public static class ImportHelper
    {
        public static Dictionary<string, int> ReadHeaderMap(IXLWorksheet sheet, int headerRow = 1)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (int c = 1; c <= lastCol; c++)
            {
                var raw = sheet.Cell(headerRow, c).GetString().Trim();
                if (string.IsNullOrEmpty(raw)) continue;
                var key = raw.TrimEnd('*').Trim();
                if (!map.ContainsKey(key)) map[key] = c;
                if (!map.ContainsKey(raw)) map[raw] = c;
            }
            return map;
        }

        public static string? GetStr(IXLWorksheet sheet, int rowNum, Dictionary<string, int> map, string key)
        {
            int col;
            if (!map.TryGetValue(key, out col) && !map.TryGetValue(key + "*", out col))
                return null;
            var cell = sheet.Cell(rowNum, col);
            if (cell.IsEmpty()) return null;
            var s = cell.GetString().Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }

        public static DateTime? GetDate(IXLWorksheet sheet, int rowNum, Dictionary<string, int> map, string key)
        {
            int col;
            if (!map.TryGetValue(key, out col) && !map.TryGetValue(key + "*", out col))
                return null;
            var cell = sheet.Cell(rowNum, col);
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime();

            var s = cell.GetString().Trim();
            if (string.IsNullOrEmpty(s)) return null;
            string[] formats = { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd-MM-yyyy", "yyyy/MM/dd" };
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;
            return null;
        }

        public static int? GetInt(IXLWorksheet sheet, int rowNum, Dictionary<string, int> map, string key)
        {
            int col;
            if (!map.TryGetValue(key, out col) && !map.TryGetValue(key + "*", out col))
                return null;
            var cell = sheet.Cell(rowNum, col);
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.Number) return (int)cell.GetDouble();
            if (int.TryParse(cell.GetString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
                return i;
            return null;
        }

        public static decimal? GetDecimal(IXLWorksheet sheet, int rowNum, Dictionary<string, int> map, string key)
        {
            int col;
            if (!map.TryGetValue(key, out col) && !map.TryGetValue(key + "*", out col))
                return null;
            var cell = sheet.Cell(rowNum, col);
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.Number) return (decimal)cell.GetDouble();
            if (decimal.TryParse(cell.GetString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            return null;
        }

        public static bool? GetBool(IXLWorksheet sheet, int rowNum, Dictionary<string, int> map, string key)
        {
            int col;
            if (!map.TryGetValue(key, out col) && !map.TryGetValue(key + "*", out col))
                return null;
            var cell = sheet.Cell(rowNum, col);
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.Boolean) return cell.GetBoolean();
            var s = cell.GetString().Trim().ToUpperInvariant();
            return s switch
            {
                "TRUE" or "1" or "YES" or "Y" or "ACTIVE"   => true,
                "FALSE" or "0" or "NO" or "N" or "INACTIVE" => false,
                _ => (bool?)null
            };
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try { var _ = new System.Net.Mail.MailAddress(email); return true; }
            catch { return false; }
        }

        public static IActionResult ServeTemplate(IWebHostEnvironment env, string fileName)
        {
            var path = Path.Combine(env.WebRootPath ?? "wwwroot", "templates", fileName);
            if (!System.IO.File.Exists(path))
                return new NotFoundObjectResult(new { success = false, message = $"Template '{fileName}' not found." });

            var bytes = System.IO.File.ReadAllBytes(path);
            return new FileContentResult(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            {
                FileDownloadName = fileName
            };
        }

        public static IActionResult? ValidateFile(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return new BadRequestObjectResult(new { success = false, message = "No file uploaded." });
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xlsm")
                return new BadRequestObjectResult(new { success = false, message = "Only .xlsx or .xlsm files are allowed." });
            return null;
        }
    }

    // DTO chung cho error response - dùng cho cả 4 controller
    public class ImportRowError
    {
        public int          Row    { get; set; }
        public string?      Key    { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
