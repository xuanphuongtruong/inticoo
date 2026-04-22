using System.Globalization;

namespace InticooInspection.Client.Helpers
{
    /// <summary>
    /// Static country → international dial-code mapping (ITU-T E.164 country codes).
    /// Used to auto-prefix mobile fields across forms (Customer, Vendor, Inspector, etc.).
    ///
    /// Matching is case-insensitive and also tries common aliases
    /// (e.g. "USA" → "United States", "UK" → "United Kingdom").
    /// </summary>
    public static class CountryDialCodes
    {
        /// <summary>
        /// Returns the dial code for a country name (e.g. "Vietnam" → "+84"),
        /// or null if the country isn't known.
        /// </summary>
        public static string? GetDialCode(string? countryName)
        {
            if (string.IsNullOrWhiteSpace(countryName)) return null;
            var key = countryName.Trim();

            if (_map.TryGetValue(key, out var code)) return code;

            // Try aliases (USA, UK, UAE, etc.)
            if (_aliases.TryGetValue(key, out var canonical) && _map.TryGetValue(canonical, out code))
                return code;

            return null;
        }

        /// <summary>
        /// When the user switches country, swap the dial-code prefix at the
        /// beginning of the mobile string, preserving the digits the user typed.
        ///
        /// Rules:
        ///  - If mobile is empty → returns just the new prefix ("+84 ")
        ///  - If mobile starts with any known dial code (+X, +XX, +XXX) → replace it
        ///  - Otherwise keeps the existing number intact and prepends the new prefix
        ///    (only if the mobile doesn't already start with '+')
        ///  - If the new country has no known code → leaves mobile unchanged
        /// </summary>
        public static string ApplyDialCode(string? mobile, string? newCountryName)
        {
            var newCode = GetDialCode(newCountryName);
            mobile = (mobile ?? string.Empty).TrimStart();

            // No known code for this country → leave mobile alone
            if (newCode == null) return mobile;

            // Empty mobile → just the prefix (with trailing space for nice UX)
            if (string.IsNullOrWhiteSpace(mobile)) return newCode + " ";

            // If mobile starts with '+', try to detect the old dial code and replace it
            if (mobile.StartsWith("+"))
            {
                // Grab the longest matching dial code at the start (1-4 digits after '+')
                var detected = DetectDialCodePrefix(mobile);
                if (detected != null)
                {
                    // Strip prefix + any whitespace/dash after it
                    var rest = mobile.Substring(detected.Length).TrimStart(' ', '-');
                    return string.IsNullOrEmpty(rest) ? newCode + " " : newCode + " " + rest;
                }

                // Starts with '+' but we can't find a known code → replace first '+...'
                // block with the new code (best effort)
                var idx = 1;
                while (idx < mobile.Length && char.IsDigit(mobile[idx])) idx++;
                var tail = mobile.Substring(idx).TrimStart(' ', '-');
                return string.IsNullOrEmpty(tail) ? newCode + " " : newCode + " " + tail;
            }

            // Mobile doesn't start with '+' → just prepend the new code
            return newCode + " " + mobile;
        }

        /// <summary>
        /// Look at the start of the mobile string and return the longest
        /// known dial code (e.g. "+1", "+84", "+852") if it matches, else null.
        /// </summary>
        private static string? DetectDialCodePrefix(string mobile)
        {
            // Try longest match first (4 digits → 1 digit)
            for (int len = 4; len >= 1; len--)
            {
                if (mobile.Length < len + 1) continue;
                var candidate = mobile.Substring(0, len + 1); // includes '+'
                // Validate: '+' then `len` digits
                bool ok = candidate[0] == '+';
                for (int i = 1; i <= len && ok; i++) if (!char.IsDigit(candidate[i])) ok = false;
                if (!ok) continue;
                if (_knownCodes.Contains(candidate)) return candidate;
            }
            return null;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Data: country → dial code (E.164). Names match the list returned by
        //  /api/geo/countries (standard English names).
        // ──────────────────────────────────────────────────────────────────────
        private static readonly Dictionary<string, string> _map =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["Afghanistan"]              = "+93",
            ["Albania"]                  = "+355",
            ["Algeria"]                  = "+213",
            ["American Samoa"]           = "+1684",
            ["Andorra"]                  = "+376",
            ["Angola"]                   = "+244",
            ["Anguilla"]                 = "+1264",
            ["Antigua and Barbuda"]      = "+1268",
            ["Argentina"]                = "+54",
            ["Armenia"]                  = "+374",
            ["Aruba"]                    = "+297",
            ["Australia"]                = "+61",
            ["Austria"]                  = "+43",
            ["Azerbaijan"]               = "+994",
            ["Bahamas"]                  = "+1242",
            ["Bahrain"]                  = "+973",
            ["Bangladesh"]               = "+880",
            ["Barbados"]                 = "+1246",
            ["Belarus"]                  = "+375",
            ["Belgium"]                  = "+32",
            ["Belize"]                   = "+501",
            ["Benin"]                    = "+229",
            ["Bermuda"]                  = "+1441",
            ["Bhutan"]                   = "+975",
            ["Bolivia"]                  = "+591",
            ["Bosnia and Herzegovina"]   = "+387",
            ["Botswana"]                 = "+267",
            ["Brazil"]                   = "+55",
            ["British Virgin Islands"]   = "+1284",
            ["Brunei"]                   = "+673",
            ["Bulgaria"]                 = "+359",
            ["Burkina Faso"]             = "+226",
            ["Burundi"]                  = "+257",
            ["Cambodia"]                 = "+855",
            ["Cameroon"]                 = "+237",
            ["Canada"]                   = "+1",
            ["Cape Verde"]               = "+238",
            ["Cayman Islands"]           = "+1345",
            ["Central African Republic"] = "+236",
            ["Chad"]                     = "+235",
            ["Chile"]                    = "+56",
            ["China"]                    = "+86",
            ["Colombia"]                 = "+57",
            ["Comoros"]                  = "+269",
            ["Congo"]                    = "+242",
            ["Cook Islands"]             = "+682",
            ["Costa Rica"]               = "+506",
            ["Croatia"]                  = "+385",
            ["Cuba"]                     = "+53",
            ["Curacao"]                  = "+599",
            ["Cyprus"]                   = "+357",
            ["Czech Republic"]           = "+420",
            ["Czechia"]                  = "+420",
            ["Democratic Republic of the Congo"] = "+243",
            ["Denmark"]                  = "+45",
            ["Djibouti"]                 = "+253",
            ["Dominica"]                 = "+1767",
            ["Dominican Republic"]       = "+1809",
            ["Ecuador"]                  = "+593",
            ["Egypt"]                    = "+20",
            ["El Salvador"]              = "+503",
            ["Equatorial Guinea"]        = "+240",
            ["Eritrea"]                  = "+291",
            ["Estonia"]                  = "+372",
            ["Eswatini"]                 = "+268",
            ["Ethiopia"]                 = "+251",
            ["Falkland Islands"]         = "+500",
            ["Faroe Islands"]            = "+298",
            ["Fiji"]                     = "+679",
            ["Finland"]                  = "+358",
            ["France"]                   = "+33",
            ["French Guiana"]            = "+594",
            ["French Polynesia"]         = "+689",
            ["Gabon"]                    = "+241",
            ["Gambia"]                   = "+220",
            ["Georgia"]                  = "+995",
            ["Germany"]                  = "+49",
            ["Ghana"]                    = "+233",
            ["Gibraltar"]                = "+350",
            ["Greece"]                   = "+30",
            ["Greenland"]                = "+299",
            ["Grenada"]                  = "+1473",
            ["Guadeloupe"]               = "+590",
            ["Guam"]                     = "+1671",
            ["Guatemala"]                = "+502",
            ["Guernsey"]                 = "+44",
            ["Guinea"]                   = "+224",
            ["Guinea-Bissau"]            = "+245",
            ["Guyana"]                   = "+592",
            ["Haiti"]                    = "+509",
            ["Honduras"]                 = "+504",
            ["Hong Kong"]                = "+852",
            ["Hungary"]                  = "+36",
            ["Iceland"]                  = "+354",
            ["India"]                    = "+91",
            ["Indonesia"]                = "+62",
            ["Iran"]                     = "+98",
            ["Iraq"]                     = "+964",
            ["Ireland"]                  = "+353",
            ["Isle of Man"]              = "+44",
            ["Israel"]                   = "+972",
            ["Italy"]                    = "+39",
            ["Ivory Coast"]              = "+225",
            ["Jamaica"]                  = "+1876",
            ["Japan"]                    = "+81",
            ["Jersey"]                   = "+44",
            ["Jordan"]                   = "+962",
            ["Kazakhstan"]               = "+7",
            ["Kenya"]                    = "+254",
            ["Kiribati"]                 = "+686",
            ["Kosovo"]                   = "+383",
            ["Kuwait"]                   = "+965",
            ["Kyrgyzstan"]               = "+996",
            ["Laos"]                     = "+856",
            ["Latvia"]                   = "+371",
            ["Lebanon"]                  = "+961",
            ["Lesotho"]                  = "+266",
            ["Liberia"]                  = "+231",
            ["Libya"]                    = "+218",
            ["Liechtenstein"]            = "+423",
            ["Lithuania"]                = "+370",
            ["Luxembourg"]               = "+352",
            ["Macao"]                    = "+853",
            ["Macau"]                    = "+853",
            ["Madagascar"]               = "+261",
            ["Malawi"]                   = "+265",
            ["Malaysia"]                 = "+60",
            ["Maldives"]                 = "+960",
            ["Mali"]                     = "+223",
            ["Malta"]                    = "+356",
            ["Marshall Islands"]         = "+692",
            ["Martinique"]               = "+596",
            ["Mauritania"]               = "+222",
            ["Mauritius"]                = "+230",
            ["Mayotte"]                  = "+262",
            ["Mexico"]                   = "+52",
            ["Micronesia"]               = "+691",
            ["Moldova"]                  = "+373",
            ["Monaco"]                   = "+377",
            ["Mongolia"]                 = "+976",
            ["Montenegro"]               = "+382",
            ["Montserrat"]               = "+1664",
            ["Morocco"]                  = "+212",
            ["Mozambique"]               = "+258",
            ["Myanmar"]                  = "+95",
            ["Namibia"]                  = "+264",
            ["Nauru"]                    = "+674",
            ["Nepal"]                    = "+977",
            ["Netherlands"]              = "+31",
            ["New Caledonia"]            = "+687",
            ["New Zealand"]              = "+64",
            ["Nicaragua"]                = "+505",
            ["Niger"]                    = "+227",
            ["Nigeria"]                  = "+234",
            ["Niue"]                     = "+683",
            ["North Korea"]              = "+850",
            ["North Macedonia"]          = "+389",
            ["Northern Mariana Islands"] = "+1670",
            ["Norway"]                   = "+47",
            ["Oman"]                     = "+968",
            ["Pakistan"]                 = "+92",
            ["Palau"]                    = "+680",
            ["Palestine"]                = "+970",
            ["Panama"]                   = "+507",
            ["Papua New Guinea"]         = "+675",
            ["Paraguay"]                 = "+595",
            ["Peru"]                     = "+51",
            ["Philippines"]              = "+63",
            ["Poland"]                   = "+48",
            ["Portugal"]                 = "+351",
            ["Puerto Rico"]              = "+1787",
            ["Qatar"]                    = "+974",
            ["Reunion"]                  = "+262",
            ["Romania"]                  = "+40",
            ["Russia"]                   = "+7",
            ["Rwanda"]                   = "+250",
            ["Saint Kitts and Nevis"]    = "+1869",
            ["Saint Lucia"]              = "+1758",
            ["Saint Vincent and the Grenadines"] = "+1784",
            ["Samoa"]                    = "+685",
            ["San Marino"]               = "+378",
            ["Sao Tome and Principe"]    = "+239",
            ["Saudi Arabia"]             = "+966",
            ["Senegal"]                  = "+221",
            ["Serbia"]                   = "+381",
            ["Seychelles"]               = "+248",
            ["Sierra Leone"]             = "+232",
            ["Singapore"]                = "+65",
            ["Sint Maarten"]             = "+1721",
            ["Slovakia"]                 = "+421",
            ["Slovenia"]                 = "+386",
            ["Solomon Islands"]          = "+677",
            ["Somalia"]                  = "+252",
            ["South Africa"]             = "+27",
            ["South Korea"]              = "+82",
            ["South Sudan"]              = "+211",
            ["Spain"]                    = "+34",
            ["Sri Lanka"]                = "+94",
            ["Sudan"]                    = "+249",
            ["Suriname"]                 = "+597",
            ["Sweden"]                   = "+46",
            ["Switzerland"]              = "+41",
            ["Syria"]                    = "+963",
            ["Taiwan"]                   = "+886",
            ["Tajikistan"]               = "+992",
            ["Tanzania"]                 = "+255",
            ["Thailand"]                 = "+66",
            ["Timor-Leste"]              = "+670",
            ["Togo"]                     = "+228",
            ["Tonga"]                    = "+676",
            ["Trinidad and Tobago"]      = "+1868",
            ["Tunisia"]                  = "+216",
            ["Turkey"]                   = "+90",
            ["Turkmenistan"]             = "+993",
            ["Turks and Caicos Islands"] = "+1649",
            ["Tuvalu"]                   = "+688",
            ["Uganda"]                   = "+256",
            ["Ukraine"]                  = "+380",
            ["United Arab Emirates"]     = "+971",
            ["United Kingdom"]           = "+44",
            ["United States"]            = "+1",
            ["Uruguay"]                  = "+598",
            ["Uzbekistan"]               = "+998",
            ["Vanuatu"]                  = "+678",
            ["Vatican City"]             = "+379",
            ["Venezuela"]                = "+58",
            ["Vietnam"]                  = "+84",
            ["Viet Nam"]                 = "+84",
            ["Wallis and Futuna"]        = "+681",
            ["Yemen"]                    = "+967",
            ["Zambia"]                   = "+260",
            ["Zimbabwe"]                 = "+263",
        };

        private static readonly Dictionary<string, string> _aliases =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["USA"]                      = "United States",
            ["U.S.A."]                   = "United States",
            ["US"]                       = "United States",
            ["U.S."]                     = "United States",
            ["UK"]                       = "United Kingdom",
            ["U.K."]                     = "United Kingdom",
            ["Great Britain"]            = "United Kingdom",
            ["England"]                  = "United Kingdom",
            ["UAE"]                      = "United Arab Emirates",
            ["Republic of Korea"]        = "South Korea",
            ["Korea, Republic of"]       = "South Korea",
            ["Korea"]                    = "South Korea",
            ["DPRK"]                     = "North Korea",
            ["Russian Federation"]       = "Russia",
            ["P.R.C."]                   = "China",
            ["PRC"]                      = "China",
            ["Mainland China"]           = "China",
            ["R.O.C."]                   = "Taiwan",
            ["ROC"]                      = "Taiwan",
            ["Holland"]                  = "Netherlands",
            ["The Netherlands"]          = "Netherlands",
            ["Hong Kong SAR"]            = "Hong Kong",
            ["Macau SAR"]                = "Macao",
            ["Myanmar (Burma)"]          = "Myanmar",
            ["Burma"]                    = "Myanmar",
            ["Côte d'Ivoire"]            = "Ivory Coast",
            ["Cote d'Ivoire"]            = "Ivory Coast",
            ["Swaziland"]                = "Eswatini",
            ["Czechia"]                  = "Czech Republic",
            ["Cape Verde"]               = "Cape Verde",
            ["Cabo Verde"]               = "Cape Verde",
            ["Macedonia"]                = "North Macedonia",
            ["Republic of North Macedonia"] = "North Macedonia",
        };

        // Pre-computed set of all codes for fast prefix detection
        private static readonly HashSet<string> _knownCodes =
            new(_map.Values, StringComparer.Ordinal);
    }
}
