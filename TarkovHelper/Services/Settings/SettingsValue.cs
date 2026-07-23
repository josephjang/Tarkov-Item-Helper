using System.Globalization;

namespace TarkovHelper.Services.Settings;

/// <summary>
/// Culture-safe formatting/parsing for settings persisted as strings in user_data.db.
/// Doubles are always WRITTEN in the invariant format ("1.5"), so a value saved on one
/// machine/locale reads back identically on any other (config migration, comma-decimal
/// locales such as de-DE/ko-KR-with-custom-separators, CI runners). READS try the
/// invariant format first and fall back to the current culture so values written
/// before this convention (e.g. "1,5" from a comma-decimal locale) still load.
/// </summary>
public static class SettingsValue
{
    /// <summary>Formats a double for persistence in the invariant culture.</summary>
    public static string FormatDouble(double value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses a persisted double: invariant format first, then a current-culture
    /// fallback for legacy values. NumberStyles.Float (no thousands separators), so
    /// "1.5" can never mis-parse as 15 via a group-separator reading.
    /// </summary>
    public static bool TryParseDouble(string? value, out double result)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return true;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
    }
}
