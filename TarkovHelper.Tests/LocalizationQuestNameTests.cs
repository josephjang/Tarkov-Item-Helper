using TarkovHelper.Models;
using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Covers the shared quest-name localization entry points (PRD Root Cause 4).
/// Uses the pure static cores so no LocalizationService/DB instance is required.
/// </summary>
public class LocalizationQuestNameTests
{
    private static TarkovTask Task(string en, string? ko = null, string? ja = null)
        => new() { Name = en, NameKo = ko, NameJa = ja };

    [Fact]
    public void Ko_uses_korean_when_present()
        => Assert.Equal("데뷔", LocalizationService.GetQuestName(AppLanguage.KO, Task("Debut", ko: "데뷔")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ko_falls_back_to_english_when_missing_empty_or_whitespace(string? ko)
        => Assert.Equal("Debut", LocalizationService.GetQuestName(AppLanguage.KO, Task("Debut", ko: ko)));

    [Fact]
    public void Ja_uses_japanese_when_present()
        => Assert.Equal("デビュー", LocalizationService.GetQuestName(AppLanguage.JA, Task("Debut", ja: "デビュー")));

    [Fact]
    public void En_always_uses_english()
        => Assert.Equal("Debut", LocalizationService.GetQuestName(AppLanguage.EN, Task("Debut", ko: "데뷔")));

    [Fact]
    public void DisplayName_ko_shows_english_subtitle_when_translated()
    {
        var (name, subtitle, show) = LocalizationService.GetQuestDisplayName(AppLanguage.KO, Task("Debut", ko: "데뷔"));
        Assert.Equal("데뷔", name);
        Assert.Equal("Debut", subtitle);
        Assert.True(show);
    }

    [Fact]
    public void DisplayName_ko_without_translation_has_no_subtitle()
    {
        var (name, subtitle, show) = LocalizationService.GetQuestDisplayName(AppLanguage.KO, Task("Debut"));
        Assert.Equal("Debut", name);
        Assert.Equal(string.Empty, subtitle);
        Assert.False(show);
    }

    [Fact]
    public void DisplayName_en_has_no_subtitle()
    {
        var (name, subtitle, show) = LocalizationService.GetQuestDisplayName(AppLanguage.EN, Task("Debut", ko: "데뷔"));
        Assert.Equal("Debut", name);
        Assert.Equal(string.Empty, subtitle);
        Assert.False(show);
    }
}
