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

    /// <summary>Pure, testable core of <see cref="GetNameComparer()"/>.</summary>
    public static StringComparer GetNameComparer(AppLanguage lang) => lang switch
    {
        AppLanguage.KO => StringComparer.Create(new CultureInfo("ko-KR"), ignoreCase: true),
        AppLanguage.JA => StringComparer.Create(new CultureInfo("ja-JP"), ignoreCase: true),
        _ => StringComparer.Create(new CultureInfo("en-US"), ignoreCase: true)
    };

    #endregion
}
