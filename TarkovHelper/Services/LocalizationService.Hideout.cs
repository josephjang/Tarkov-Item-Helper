using System;
using System.Globalization;

namespace TarkovHelper.Services;

/// <summary>
/// Hideout-related localization helpers for LocalizationService.
/// </summary>
public partial class LocalizationService
{
    #region Localized Name Ordering

    /// <summary>
    /// Returns a culture-aware, case-insensitive name comparer for the current language.
    /// Used to order localized display names so the hideout list matches EFT's in-game ordering
    /// (English alphabetical, Korean Hangul collation, Japanese kana order).
    /// </summary>
    public StringComparer GetNameComparer() => GetNameComparer(CurrentLanguage);

    // Cached, immutable comparers: GetNameComparer runs on every keystroke in HideoutPage.ApplyFilters(),
    // so create each culture's comparer once instead of per call. Resolve cultures defensively so a host
    // missing one cannot crash static initialization (TypeInitializationException); fall back to ordinal.
    private static readonly StringComparer KoNameComparer = CreateNameComparer("ko-KR");
    private static readonly StringComparer JaNameComparer = CreateNameComparer("ja-JP");
    private static readonly StringComparer EnNameComparer = CreateNameComparer("en-US");

    private static StringComparer CreateNameComparer(string culture)
    {
        try
        {
            return StringComparer.Create(CultureInfo.GetCultureInfo(culture), ignoreCase: true);
        }
        catch (CultureNotFoundException)
        {
            return StringComparer.OrdinalIgnoreCase;
        }
    }

    /// <summary>Pure, testable core of <see cref="GetNameComparer()"/>.</summary>
    public static StringComparer GetNameComparer(AppLanguage lang) => lang switch
    {
        AppLanguage.KO => KoNameComparer,
        AppLanguage.JA => JaNameComparer,
        _ => EnNameComparer
    };

    #endregion
}
