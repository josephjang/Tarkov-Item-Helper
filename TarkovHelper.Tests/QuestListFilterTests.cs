using TarkovHelper.Models;
using TarkovHelper.Pages;

namespace TarkovHelper.Tests;

/// <summary>
/// Guards the quest-list filter predicate (QuestListFilter.Matches) that
/// QuestListPage.ApplyFilters and the preserve-filters-on-navigation change lean on
/// (see feature-preserve-quest-filters-on-navigation.spec.md). The predicate was
/// extracted verbatim from the page's inline lambda; these tests pin its semantics:
/// status-tag mapping (Locked includes LevelLocked), the faction/Unavailable
/// exception, multi-language search, and each single-criterion rejection.
/// </summary>
public sealed class QuestListFilterTests
{
    /// <summary>No-filter criteria matching the filter bar's "everything visible" state.</summary>
    private static QuestFilterCriteria AllCriteria(
        string searchText = "",
        bool kappaOnly = false,
        bool itemRequired = false,
        string trader = "",
        string map = "",
        string statusTag = "All",
        string? faction = null)
        => new(searchText, kappaOnly, itemRequired, trader, map, statusTag, faction);

    private static QuestViewModel Vm(
        string name = "Debut",
        string? nameKo = null,
        string? nameJa = null,
        QuestStatus status = QuestStatus.Active,
        string trader = "Prapor",
        List<string>? maps = null,
        bool reqKappa = false,
        List<QuestItem>? requiredItems = null,
        string? faction = null)
        => new()
        {
            Task = new TarkovTask
            {
                Name = name,
                NameKo = nameKo,
                NameJa = nameJa,
                Trader = trader,
                Maps = maps,
                ReqKappa = reqKappa,
                RequiredItems = requiredItems,
                Faction = faction,
                NormalizedName = name.ToLowerInvariant().Replace(' ', '-'),
            },
            Status = status,
        };

    [Fact]
    public void Empty_criteria_pass_everything()
    {
        foreach (QuestStatus status in Enum.GetValues<QuestStatus>())
        {
            Assert.True(QuestListFilter.Matches(Vm(status: status), AllCriteria()));
        }
    }

    [Theory]
    [InlineData(QuestStatus.Locked, true)]
    [InlineData(QuestStatus.LevelLocked, true)]
    [InlineData(QuestStatus.Active, false)]
    [InlineData(QuestStatus.Done, false)]
    [InlineData(QuestStatus.Failed, false)]
    [InlineData(QuestStatus.Unavailable, false)]
    public void Locked_tag_includes_both_locked_and_levellocked(QuestStatus status, bool expected)
    {
        Assert.Equal(expected,
            QuestListFilter.Matches(Vm(status: status), AllCriteria(statusTag: "Locked")));
    }

    [Theory]
    [InlineData("Active", QuestStatus.Active)]
    [InlineData("Done", QuestStatus.Done)]
    [InlineData("Failed", QuestStatus.Failed)]
    [InlineData("Unavailable", QuestStatus.Unavailable)]
    public void Status_tag_matches_only_its_own_status(string tag, QuestStatus matching)
    {
        foreach (QuestStatus status in Enum.GetValues<QuestStatus>())
        {
            Assert.Equal(status == matching,
                QuestListFilter.Matches(Vm(status: status), AllCriteria(statusTag: tag)));
        }
    }

    [Theory]
    [InlineData("debut", true)]   // EN, case-insensitive
    [InlineData("  Debut  ", true)]  // surrounding whitespace is trimmed
    [InlineData("데뷔", true)]     // KO name
    [InlineData("デビュー", true)] // JA name
    [InlineData("shortage", false)]
    public void Search_matches_any_language_name(string search, bool expected)
    {
        var vm = Vm(name: "Debut", nameKo: "데뷔", nameJa: "デビュー");
        Assert.Equal(expected, QuestListFilter.Matches(vm, AllCriteria(searchText: search)));
    }

    [Fact]
    public void Search_with_missing_localized_names_matches_english_only()
    {
        var vm = Vm(name: "Debut", nameKo: null, nameJa: null);
        Assert.True(QuestListFilter.Matches(vm, AllCriteria(searchText: "deb")));
        Assert.False(QuestListFilter.Matches(vm, AllCriteria(searchText: "데뷔")));
    }

    [Fact]
    public void Kappa_only_rejects_non_kappa_quests()
    {
        Assert.False(QuestListFilter.Matches(Vm(reqKappa: false), AllCriteria(kappaOnly: true)));
        Assert.True(QuestListFilter.Matches(Vm(reqKappa: true), AllCriteria(kappaOnly: true)));
    }

    [Fact]
    public void Item_required_rejects_quests_without_required_items()
    {
        Assert.False(QuestListFilter.Matches(
            Vm(requiredItems: null), AllCriteria(itemRequired: true)));
        Assert.False(QuestListFilter.Matches(
            Vm(requiredItems: new List<QuestItem>()), AllCriteria(itemRequired: true)));
        Assert.True(QuestListFilter.Matches(
            Vm(requiredItems: new List<QuestItem> { new() }), AllCriteria(itemRequired: true)));
    }

    [Fact]
    public void Trader_filter_is_exact_and_case_sensitive()
    {
        var vm = Vm(trader: "Prapor");
        Assert.True(QuestListFilter.Matches(vm, AllCriteria(trader: "Prapor")));
        Assert.False(QuestListFilter.Matches(vm, AllCriteria(trader: "Therapist")));
        // The trader combo is populated from the same task data, so exact match is the
        // contract — a differently-cased value must not match.
        Assert.False(QuestListFilter.Matches(vm, AllCriteria(trader: "prapor")));
    }

    [Fact]
    public void Map_filter_matches_any_of_the_quest_maps_case_insensitively()
    {
        var vm = Vm(maps: new List<string> { "Customs", "Factory" });
        Assert.True(QuestListFilter.Matches(vm, AllCriteria(map: "customs")));
        Assert.True(QuestListFilter.Matches(vm, AllCriteria(map: "Factory")));
        Assert.False(QuestListFilter.Matches(vm, AllCriteria(map: "Shoreline")));
        Assert.False(QuestListFilter.Matches(Vm(maps: null), AllCriteria(map: "Customs")));
    }

    [Theory]
    [InlineData("bear", "usec", "All", false)]     // other faction is hidden...
    [InlineData("bear", "usec", "Unavailable", true)] // ...except under the Unavailable tag
    [InlineData("bear", "bear", "All", true)]      // own faction always passes
    [InlineData(null, "usec", "All", true)]        // no faction selected: nothing hidden
    [InlineData("bear", null, "All", true)]        // faction-neutral quest always passes
    public void Faction_filter_hides_other_faction_except_under_unavailable_tag(
        string? selectedFaction, string? questFaction, string statusTag, bool expected)
    {
        // Status filter itself must not reject: use a status the tag accepts.
        var status = statusTag == "Unavailable" ? QuestStatus.Unavailable : QuestStatus.Active;
        var vm = Vm(status: status, faction: questFaction);
        Assert.Equal(expected, QuestListFilter.Matches(
            vm, AllCriteria(statusTag: statusTag, faction: selectedFaction)));
    }

    [Fact]
    public void Faction_comparison_is_case_insensitive()
    {
        var vm = Vm(faction: "BEAR");
        Assert.True(QuestListFilter.Matches(vm, AllCriteria(faction: "bear")));
        Assert.False(QuestListFilter.Matches(vm, AllCriteria(faction: "usec")));
    }

    [Fact]
    public void Unknown_status_tag_matches_nothing_instead_of_throwing()
    {
        // A typo'd ComboBox tag or a careless new caller of the public predicate must
        // degrade to an empty result, not throw ArgumentException on the UI thread.
        foreach (QuestStatus status in Enum.GetValues<QuestStatus>())
        {
            Assert.False(QuestListFilter.Matches(
                Vm(status: status), AllCriteria(statusTag: "NoSuchStatus")));
        }
    }

    [Fact]
    public void Search_text_is_normalized_once_at_construction()
    {
        Assert.Equal("debut", AllCriteria(searchText: "  DeBuT  ").NormalizedSearchText);
        Assert.Equal(string.Empty, AllCriteria().NormalizedSearchText);
    }

    [Fact]
    public void Criteria_combine_with_and_semantics()
    {
        var vm = Vm(name: "Shortage", status: QuestStatus.Active, trader: "Therapist",
            maps: new List<string> { "Customs" }, reqKappa: true,
            requiredItems: new List<QuestItem> { new() });

        Assert.True(QuestListFilter.Matches(vm, AllCriteria(
            searchText: "short", kappaOnly: true, itemRequired: true,
            trader: "Therapist", map: "Customs", statusTag: "Active")));

        // Flipping any single criterion rejects.
        Assert.False(QuestListFilter.Matches(vm, AllCriteria(
            searchText: "short", kappaOnly: true, itemRequired: true,
            trader: "Therapist", map: "Customs", statusTag: "Done")));
        Assert.False(QuestListFilter.Matches(vm, AllCriteria(
            searchText: "short", kappaOnly: true, itemRequired: true,
            trader: "Prapor", map: "Customs", statusTag: "Active")));
    }
}
