using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Guards the culture-aware comparer that orders the hideout list by localized name so it matches
/// EFT's in-game ordering (see the feature-hideout-localized-sort PRD). Exercises the pure static
/// <see cref="LocalizationService.GetNameComparer(AppLanguage)"/> core, so no service/DB is needed.
/// </summary>
public class HideoutSortOrderTests
{
    private static string[] Sorted(AppLanguage lang, params string[] names)
        => names.OrderBy(n => n, LocalizationService.GetNameComparer(lang)).ToArray();

    [Fact]
    public void English_sorts_alphabetically()
        => Assert.Equal(
            new[] { "Air Filtering Unit", "Bitcoin Farm", "Workbench" },
            Sorted(AppLanguage.EN, "Workbench", "Air Filtering Unit", "Bitcoin Farm"));

    [Fact]
    public void English_comparer_is_case_insensitive_not_ordinal()
    {
        // StringComparer.Ordinal would place uppercase "Banana" (0x42) before lowercase "apple" (0x61);
        // a culture-aware, case-insensitive comparer orders a before b. Guards against an Ordinal regression.
        var cmp = LocalizationService.GetNameComparer(AppLanguage.EN);
        Assert.True(cmp.Compare("apple", "Banana") < 0);
    }

    [Fact]
    public void Korean_sorts_by_hangul_collation()
        // By English name (Water Collector, Workbench) these sort Water < Workbench; the Korean
        // collation reverses that pair, proving the list is ordered by the localized name.
        => Assert.Equal(
            new[] { "공기 청정 장치", "비트코인 농장", "작업대", "정수기" },
            Sorted(AppLanguage.KO, "정수기", "공기 청정 장치", "작업대", "비트코인 농장"));

    [Fact]
    public void Japanese_sorts_by_kana_order()
        // Japanese kana (gojuon) order differs from the English order of the same modules
        // (English: Bitcoin Farm < Generator < Workbench).
        => Assert.Equal(
            new[] { "ジェネレーター", "ビットコインファーム", "ワークベンチ" },
            Sorted(AppLanguage.JA, "ワークベンチ", "ビットコインファーム", "ジェネレーター"));
}
