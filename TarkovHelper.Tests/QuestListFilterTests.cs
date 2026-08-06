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

    // The production chip-tag list (All first) — the count tests iterate exactly the
    // tags UpdateStatusChips hands to CountByStatusTag, so the All chip's count is
    // covered by every one of them.
    private static readonly string[] AllStatusTags = QuestStatusTags.ChipTags;

    [Fact]
    public void Chip_counts_group_by_tag_with_locked_including_levellocked()
    {
        var vms = new List<QuestViewModel>
        {
            Vm(name: "A1", status: QuestStatus.Active),
            Vm(name: "A2", status: QuestStatus.Active),
            Vm(name: "L1", status: QuestStatus.Locked),
            Vm(name: "LL1", status: QuestStatus.LevelLocked),
            Vm(name: "D1", status: QuestStatus.Done),
            Vm(name: "F1", status: QuestStatus.Failed),
            Vm(name: "U1", status: QuestStatus.Unavailable),
        };

        var counts = QuestListFilter.CountByStatusTag(
            vms, AllCriteria(statusTag: "Active"), AllStatusTags);

        Assert.Equal(2, counts["Active"]);
        Assert.Equal(2, counts["Locked"]); // Locked + LevelLocked share the chip
        Assert.Equal(1, counts["Done"]);
        Assert.Equal(1, counts["Failed"]);
        Assert.Equal(1, counts["Unavailable"]);
    }

    [Fact]
    public void Chip_counts_respect_the_non_status_criteria()
    {
        var vms = new List<QuestViewModel>
        {
            Vm(name: "Debut", status: QuestStatus.Active, trader: "Prapor"),
            Vm(name: "Shortage", status: QuestStatus.Active, trader: "Therapist"),
            Vm(name: "Checking", status: QuestStatus.Done, trader: "Prapor"),
        };

        var counts = QuestListFilter.CountByStatusTag(
            vms, AllCriteria(trader: "Prapor"), AllStatusTags);

        Assert.Equal(1, counts["Active"]); // Shortage belongs to Therapist
        Assert.Equal(1, counts["Done"]);
        Assert.Equal(0, counts["Locked"]);
    }

    [Fact]
    public void Chip_counts_do_not_depend_on_the_currently_selected_status_tag()
    {
        // Each count substitutes its own tag, so the selected status must not leak in —
        // the chips are click-previews, identical whichever chip is active now.
        var vms = new List<QuestViewModel>
        {
            Vm(name: "A", status: QuestStatus.Active),
            Vm(name: "D", status: QuestStatus.Done),
        };

        var fromActive = QuestListFilter.CountByStatusTag(vms, AllCriteria(statusTag: "Active"), AllStatusTags);
        var fromAll = QuestListFilter.CountByStatusTag(vms, AllCriteria(statusTag: "All"), AllStatusTags);

        Assert.Equal(fromActive, fromAll);
    }

    [Fact]
    public void Chip_counts_count_other_faction_quests_only_under_unavailable()
    {
        var vms = new List<QuestViewModel>
        {
            Vm(name: "Own", status: QuestStatus.Active, faction: "bear"),
            Vm(name: "Other", status: QuestStatus.Unavailable, faction: "usec"),
        };

        var counts = QuestListFilter.CountByStatusTag(
            vms, AllCriteria(faction: "bear"), AllStatusTags);

        Assert.Equal(1, counts["Active"]);
        Assert.Equal(1, counts["Unavailable"]); // the other-faction quest surfaces only here
        Assert.Equal(0, counts["Done"]);
    }

    [Fact]
    public void Chip_counts_preserve_the_normalized_search_text_through_the_with_copy()
    {
        var vms = new List<QuestViewModel>
        {
            Vm(name: "Debut", status: QuestStatus.Active),
            Vm(name: "Shortage", status: QuestStatus.Active),
        };

        // "  DeBuT  " matches only after the one-time trim+lowercase, so a per-tag
        // criteria copy that lost the precomputed NormalizedSearchText would count both
        // quests (empty search = no filter) instead of one.
        var counts = QuestListFilter.CountByStatusTag(
            vms, AllCriteria(searchText: "  DeBuT  ", statusTag: "All"), AllStatusTags);

        Assert.Equal(1, counts["Active"]);
        Assert.Equal(0, counts["Done"]);
    }

    [Fact]
    public void With_copy_carries_the_precomputed_normalized_search_text()
    {
        var criteria = AllCriteria(searchText: "  DeBuT  ", statusTag: "All");

        var copy = criteria with { StatusTag = "Done" };

        // The record's copy constructor copies the backing field; the property
        // initializer does NOT re-run. Asserted directly, because the count-based test
        // above cannot tell "copied" from "recomputed from an unchanged SearchText".
        Assert.Equal("debut", copy.NormalizedSearchText);
        Assert.Equal(criteria.NormalizedSearchText, copy.NormalizedSearchText);
    }

    [Fact]
    public void With_copy_of_SearchText_does_not_recompute_the_normalized_text()
    {
        var criteria = AllCriteria(searchText: "Debut");

        var copy = criteria with { SearchText = "Shortage" };

        // The hazard QuestFilterCriteria's doc comment warns about, pinned so a refactor
        // to a computed property cannot silently change the contract CountByStatusTag
        // depends on: SearchText moves, the normalized text does not follow it.
        Assert.Equal("Shortage", copy.SearchText);
        Assert.Equal("debut", copy.NormalizedSearchText);
    }

    [Fact]
    public void Chip_counts_equal_the_per_tag_Matches_count_for_every_tag()
    {
        // The single-pass CountByStatusTag must stay exactly "what Matches would say if
        // this tag were selected" — including the Locked+LevelLocked merge and the
        // faction/Unavailable exception, which the one-pass form evaluates separately.
        var vms = new List<QuestViewModel>
        {
            Vm(name: "Debut", status: QuestStatus.Active, trader: "Prapor", faction: "bear"),
            Vm(name: "Checking", status: QuestStatus.Active, trader: "Prapor"),
            Vm(name: "Shortage", status: QuestStatus.LevelLocked, trader: "Therapist"),
            Vm(name: "Sanitary", status: QuestStatus.Locked, trader: "Prapor"),
            Vm(name: "Delivery", status: QuestStatus.Done, trader: "Prapor"),
            Vm(name: "Bad Rep", status: QuestStatus.Failed, trader: "Prapor"),
            Vm(name: "Usec Only", status: QuestStatus.Unavailable, trader: "Prapor", faction: "usec"),
            Vm(name: "Bear Only", status: QuestStatus.Active, trader: "Prapor", faction: "usec"),
        };
        var criteria = AllCriteria(trader: "Prapor", faction: "bear", statusTag: "Active");

        var counts = QuestListFilter.CountByStatusTag(vms, criteria, AllStatusTags);

        foreach (var tag in AllStatusTags)
        {
            var expected = vms.Count(vm => QuestListFilter.Matches(vm, criteria with { StatusTag = tag }));
            Assert.Equal(expected, counts[tag]);
        }
    }

    [Fact]
    public void Chip_counts_are_zero_for_every_tag_when_no_quests_are_loaded()
    {
        var counts = QuestListFilter.CountByStatusTag(
            new List<QuestViewModel>(), AllCriteria(), AllStatusTags);

        // Every requested tag gets a key even with nothing to count — UpdateStatusChips
        // indexes counts[tag] for each chip and would throw on a missing key.
        Assert.Equal(AllStatusTags.Length, counts.Count);
        Assert.All(AllStatusTags, tag => Assert.Equal(0, counts[tag]));
    }

    [Fact]
    public void Chip_counts_are_zero_for_an_unrecognized_status_tag()
    {
        var vms = new List<QuestViewModel> { Vm(status: QuestStatus.Active) };

        var counts = QuestListFilter.CountByStatusTag(
            vms, AllCriteria(), new[] { "Active", "NotAStatus" });

        Assert.Equal(1, counts["Active"]);
        Assert.Equal(0, counts["NotAStatus"]); // matches nothing rather than throwing
    }

    [Fact]
    public void Chip_tags_start_with_All_and_cover_every_status_except_LevelLocked()
    {
        // The chips are the app's ONLY status filter, so the shared tag table must
        // stay in step with QuestStatus — a status added to the enum with no chip
        // would be unreachable. "All" leads the row: it is the direct route back to
        // the unfiltered list (see feature-quest-chip-only-status-filter.md).
        var chipTags = QuestStatusTags.ChipTags;

        Assert.Equal(QuestStatusTags.All, chipTags[0]);
        Assert.Equal(chipTags.Length, chipTags.Distinct().Count());
        foreach (var status in Enum.GetValues<QuestStatus>())
        {
            // LevelLocked deliberately shares the Locked chip (see CountByStatusTag).
            if (status == QuestStatus.LevelLocked) continue;
            Assert.Contains(status.ToString(), chipTags);
        }
    }

    [Fact]
    public void All_chip_count_is_the_All_click_preview_not_the_sum_or_the_total()
    {
        // Own-faction Active + Done, plus an other-faction quest that the faction
        // filter hides under "All" but surfaces under "Unavailable".
        var vms = new List<QuestViewModel>
        {
            Vm(name: "Own Active", status: QuestStatus.Active, faction: "bear"),
            Vm(name: "Own Done", status: QuestStatus.Done, faction: "bear"),
            Vm(name: "Other", status: QuestStatus.Unavailable, faction: "usec"),
        };

        var counts = QuestListFilter.CountByStatusTag(
            vms, AllCriteria(faction: "bear"), AllStatusTags);

        // All = what the list shows when All is selected: 2. NOT the per-chip sum
        // (1 Active + 1 Done + 1 Unavailable = 3) and NOT the raw loaded total (3).
        Assert.Equal(2, counts["All"]);
        Assert.Equal(1, counts["Active"]);
        Assert.Equal(1, counts["Done"]);
        Assert.Equal(1, counts["Unavailable"]);
    }

    [Fact]
    public void IsKnown_accepts_every_chip_tag_and_rejects_anything_else()
    {
        foreach (var tag in QuestStatusTags.ChipTags)
        {
            Assert.True(QuestStatusTags.IsKnown(tag));
        }

        Assert.False(QuestStatusTags.IsKnown("NotAStatus"));
        Assert.False(QuestStatusTags.IsKnown(""));
        Assert.False(QuestStatusTags.IsKnown(null));
        Assert.False(QuestStatusTags.IsKnown("active")); // ordinal, not case-insensitive
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
