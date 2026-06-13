using TarkovDBEditor.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Covers the quest NameKO/NameJA fallback guard (PRD Root Cause 3):
/// tarkov.dev returns the English name for untranslated tasks, so a real
/// translation must be kept while missing/empty/English-equal values become NULL.
/// </summary>
public class QuestNameMergeTests
{
    [Fact]
    public void Real_translation_is_kept()
        => Assert.Equal("사격 연습", TarkovDevDataService.ResolveLocalizedQuestName("사격 연습", "Shooting Cans"));

    [Fact]
    public void Translation_equal_to_english_becomes_null()
        => Assert.Null(TarkovDevDataService.ResolveLocalizedQuestName("First in Line", "First in Line"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_empty_or_whitespace_translation_becomes_null(string? localized)
        => Assert.Null(TarkovDevDataService.ResolveLocalizedQuestName(localized, "Some Quest"));
}
