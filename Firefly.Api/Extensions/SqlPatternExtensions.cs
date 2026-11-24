namespace Firefly.Api.Extensions;

public static class SqlPatternExtensions
{
    /// <summary>
    /// Zamienia wpisany tekst na wzorzec "zawiera" dla ILIKE, wyłączając znaki specjalne (%, _, \),
    /// żeby użytkownik wyszukiwał dosłownie to, co wpisał.
    /// </summary>
    public static string ToContainsPattern(this string value)
    {
        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }
}
