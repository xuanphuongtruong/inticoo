using System;
using System.Collections.Generic;

namespace InticooInspection.Client.Helpers
{
    /// <summary>
    /// Đổi tên quốc gia → URL ảnh cờ (flagcdn). Dùng ảnh thay vì emoji vì Windows không hiển thị emoji cờ.
    /// Không nhận ra quốc gia → trả "" (giao diện tự hiện icon địa cầu).
    /// </summary>
    public static class CountryFlag
    {
        private static readonly Dictionary<string, string> Codes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Vietnam"] = "vn", ["Viet Nam"] = "vn", ["Malaysia"] = "my", ["Indonesia"] = "id", ["Cambodia"] = "kh",
            ["China"] = "cn", ["Thailand"] = "th", ["Philippines"] = "ph", ["India"] = "in", ["Bangladesh"] = "bd",
            ["Taiwan"] = "tw", ["Hong Kong"] = "hk", ["Singapore"] = "sg", ["Myanmar"] = "mm", ["Laos"] = "la",
            ["Japan"] = "jp", ["Korea"] = "kr", ["South Korea"] = "kr", ["Pakistan"] = "pk", ["Sri Lanka"] = "lk",
            ["Turkey"] = "tr", ["United States"] = "us", ["USA"] = "us", ["United Kingdom"] = "gb", ["UK"] = "gb",
            ["Australia"] = "au", ["Germany"] = "de", ["France"] = "fr", ["Italy"] = "it", ["Spain"] = "es",
            ["Netherlands"] = "nl", ["Poland"] = "pl", ["Canada"] = "ca", ["Mexico"] = "mx", ["Brazil"] = "br",
        };

        public static string Url(string? country)
        {
            if (string.IsNullOrWhiteSpace(country)) return "";
            return Codes.TryGetValue(country.Trim(), out var c) ? $"https://flagcdn.com/w40/{c}.png" : "";
        }
    }
}
