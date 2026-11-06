using GeopoliticsGame.Core.Domain;

namespace GeopoliticsGame.Core.Data;

/// <summary>
/// Seed data for the countries collection. Populations are approximate figures from
/// https://www.worldometers.info/world-population/population-by-country/ .
/// The soldier value is NOT stored here; it is derived by <c>SoldierAllocator</c>.
/// </summary>
public static class CountryCatalogue
{
    private const long M = 1_000_000;

    public static IReadOnlyList<CountryInfo> All { get; } = new List<CountryInfo>
    {
        new("IND", "India", 1_441 * M, "🇮🇳"),
        new("CHN", "China", 1_425 * M, "🇨🇳"),
        new("USA", "United States", 341 * M, "🇺🇸"),
        new("IDN", "Indonesia", 279 * M, "🇮🇩"),
        new("PAK", "Pakistan", 245 * M, "🇵🇰"),
        new("NGA", "Nigeria", 229 * M, "🇳🇬"),
        new("BRA", "Brazil", 217 * M, "🇧🇷"),
        new("BGD", "Bangladesh", 174 * M, "🇧🇩"),
        new("RUS", "Russia", 144 * M, "🇷🇺"),
        new("ETH", "Ethiopia", 129 * M, "🇪🇹"),
        new("MEX", "Mexico", 129 * M, "🇲🇽"),
        new("JPN", "Japan", 123 * M, "🇯🇵"),
        new("PHL", "Philippines", 119 * M, "🇵🇭"),
        new("EGY", "Egypt", 114 * M, "🇪🇬"),
        new("COD", "DR Congo", 105 * M, "🇨🇩"),
        new("VNM", "Vietnam", 99 * M, "🇻🇳"),
        new("IRN", "Iran", 90 * M, "🇮🇷"),
        new("TUR", "Turkey", 86 * M, "🇹🇷"),
        new("DEU", "Germany", 83 * M, "🇩🇪"),
        new("THA", "Thailand", 72 * M, "🇹🇭"),
        new("GBR", "United Kingdom", 68 * M, "🇬🇧"),
        new("TZA", "Tanzania", 69 * M, "🇹🇿"),
        new("FRA", "France", 65 * M, "🇫🇷"),
        new("ZAF", "South Africa", 61 * M, "🇿🇦"),
        new("ITA", "Italy", 59 * M, "🇮🇹"),
        new("KEN", "Kenya", 56 * M, "🇰🇪"),
        new("KOR", "South Korea", 52 * M, "🇰🇷"),
        new("COL", "Colombia", 53 * M, "🇨🇴"),
        new("ESP", "Spain", 48 * M, "🇪🇸"),
        new("ARG", "Argentina", 46 * M, "🇦🇷"),
        new("UKR", "Ukraine", 38 * M, "🇺🇦"),
        new("CAN", "Canada", 39 * M, "🇨🇦"),
        new("MAR", "Morocco", 38 * M, "🇲🇦"),
        new("SAU", "Saudi Arabia", 37 * M, "🇸🇦"),
        new("AUS", "Australia", 27 * M, "🇦🇺"),
        new("GHA", "Ghana", 34 * M, "🇬🇭"),
        new("NLD", "Netherlands", 18 * M, "🇳🇱"),
        new("ZWE", "Zimbabwe", 17 * M, "🇿🇼"),
        new("SWE", "Sweden", 10_600_000, "🇸🇪"),
        new("PRT", "Portugal", 10_400_000, "🇵🇹"),
        new("ISR", "Israel", 9_300_000, "🇮🇱"),
        new("NAM", "Namibia", 3_000_000, "🇳🇦"),
        new("BWA", "Botswana", 2_700_000, "🇧🇼"),
        new("NOR", "Norway", 5_500_000, "🇳🇴"),
        new("NZL", "New Zealand", 5_200_000, "🇳🇿"),
        new("IRL", "Ireland", 5_100_000, "🇮🇪"),
        new("ISL", "Iceland", 390_000, "🇮🇸"),
        new("VAT", "Vatican City", 500, "🇻🇦"),
    };
}
