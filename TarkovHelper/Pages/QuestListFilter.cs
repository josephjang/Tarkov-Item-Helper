using TarkovHelper.Models;

namespace TarkovHelper.Pages
{
    /// <summary>
    /// The quest-list filter inputs as read from the filter bar controls, using the
    /// control conventions: an empty string means "no filter" for SearchText, Trader,
    /// and Map; StatusTag is a status-chip Tag ("All", "Active", "Locked", "Done",
    /// "Failed", "Unavailable"); Faction is "bear", "usec", or null for no selection.
    /// </summary>
    public sealed record QuestFilterCriteria(
        string SearchText,
        bool KappaOnly,
        bool ItemRequired,
        string Trader,
        string Map,
        string StatusTag,
        string? Faction)
    {
        /// <summary>
        /// SearchText trimmed and lower-cased once at construction, so the per-quest
        /// predicate does not re-normalize the same string for every quest. Computed
        /// from the constructor argument — a `with`-mutation of SearchText would NOT
        /// recompute it, so treat the record as the one-shot filter-bar snapshot it is.
        /// </summary>
        public string NormalizedSearchText { get; } = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// The status filter tags: the Tag values of QuestListPage's status-chip Buttons,
    /// which double as the persisted questList.statusTag value. QuestListPage.xaml
    /// declares the same strings on its chip Buttons — these constants are the single
    /// source for every C# path that names one, so a rename only has to be mirrored in
    /// that one XAML block.
    /// </summary>
    public static class QuestStatusTags
    {
        public const string All = "All";
        public const string Active = "Active";
        public const string Locked = "Locked";
        public const string Done = "Done";
        public const string Failed = "Failed";
        public const string Unavailable = "Unavailable";

        /// <summary>
        /// The tags that get a status chip, in display order. "All" comes first: with
        /// the status combo removed, the All chip is the direct route back to the
        /// unfiltered list and carries the total count (see
        /// feature-quest-chip-only-status-filter.md). Re-clicking the selected status
        /// chip also returns to All.
        /// </summary>
        public static readonly string[] ChipTags = { All, Active, Locked, Done, Failed, Unavailable };

        /// <summary>
        /// Whether the tag is one the chips (and the persisted questList.statusTag)
        /// understand — the restore-time validation home: with the combo gone there is
        /// no tag-lookup fallback to absorb an unknown persisted value, so callers
        /// widen unknown tags to <see cref="All"/> explicitly. Ordinal, like every
        /// other status-tag comparison.
        /// </summary>
        public static bool IsKnown(string? tag)
            => tag != null && ChipTags.Contains(tag, StringComparer.Ordinal);
    }

    /// <summary>
    /// The quest-list filter predicate, extracted from QuestListPage.ApplyFilters so
    /// the filter semantics are unit-testable without WPF (see QuestListFilterTests).
    /// Keep this the single place that decides whether a quest is visible under the
    /// filter bar.
    /// </summary>
    public static class QuestListFilter
    {
        public static bool Matches(QuestViewModel vm, QuestFilterCriteria criteria)
            => MatchesNonStatusCriteria(vm, criteria)
               && MatchesStatusTag(vm, criteria.StatusTag)
               && MatchesFaction(vm, criteria.Faction, criteria.StatusTag);

        /// <summary>
        /// Every criterion except status and faction — the expensive half (it lowercases
        /// the three name fields per quest). Split out so <see cref="CountByStatusTag"/>
        /// can evaluate it once per quest instead of once per quest per status tag.
        /// </summary>
        private static bool MatchesNonStatusCriteria(QuestViewModel vm, QuestFilterCriteria criteria)
        {
            // Search filter (multi-language)
            var searchText = criteria.NormalizedSearchText;
            if (!string.IsNullOrEmpty(searchText))
            {
                var matchName = vm.Task.Name?.ToLowerInvariant().Contains(searchText) == true;
                var matchKo = vm.Task.NameKo?.ToLowerInvariant().Contains(searchText) == true;
                var matchJa = vm.Task.NameJa?.ToLowerInvariant().Contains(searchText) == true;

                if (!matchName && !matchKo && !matchJa)
                    return false;
            }

            // Kappa filter
            if (criteria.KappaOnly && !vm.Task.ReqKappa)
                return false;

            // Item required filter
            if (criteria.ItemRequired && (vm.Task.RequiredItems == null || vm.Task.RequiredItems.Count == 0))
                return false;

            // Trader filter
            if (!string.IsNullOrEmpty(criteria.Trader) && vm.Task.Trader != criteria.Trader)
                return false;

            // Map filter
            if (!string.IsNullOrEmpty(criteria.Map))
            {
                if (vm.Task.Maps == null || !vm.Task.Maps.Any(m =>
                    string.Equals(m, criteria.Map, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }

        /// <summary>The status half of the predicate, for one status tag.</summary>
        private static bool MatchesStatusTag(QuestViewModel vm, string statusTag)
        {
            if (statusTag == QuestStatusTags.All)
                return true;

            // "Locked" filter now includes both Locked and LevelLocked
            if (statusTag == QuestStatusTags.Locked)
                return vm.Status == QuestStatus.Locked || vm.Status == QuestStatus.LevelLocked;

            // An unrecognized tag (a future chip-Tag typo, or a new caller of this
            // public predicate) matches nothing rather than throwing ArgumentException
            // on the UI thread mid-ApplyFilters.
            if (!Enum.TryParse<QuestStatus>(statusTag, out var statusFilter))
                return false;

            return vm.Status == statusFilter;
        }

        /// <summary>
        /// The faction half: hide quests for the other faction, EXCEPT under the
        /// "Unavailable" status filter, so faction-restricted quests stay discoverable.
        /// Depends on the status tag, hence the explicit parameter rather than the
        /// criteria's own tag — the chip counts evaluate it per candidate tag.
        /// </summary>
        private static bool MatchesFaction(QuestViewModel vm, string? faction, string statusTag)
        {
            if (string.IsNullOrEmpty(faction) || string.IsNullOrEmpty(vm.Task.Faction))
                return true;

            if (string.Equals(vm.Task.Faction, faction, StringComparison.OrdinalIgnoreCase))
                return true;

            // Only hide if NOT viewing Unavailable status
            return statusTag == QuestStatusTags.Unavailable;
        }

        /// <summary>
        /// For each status tag, the number of quests that would be visible if the status
        /// filter were set to that tag while every other criterion stays as-is — the
        /// numbers on the quest page's status chips, which are click-previews by
        /// definition (clicking a chip applies exactly the counted filter). Because of
        /// the faction/Unavailable exception in <see cref="Matches"/>, an other-faction
        /// quest is counted only under the "Unavailable" tag, so chip counts need not
        /// sum to any fixed total. "All" is itself a countable tag: its count is the
        /// All click-preview (what the list shows with no status filter), not the sum
        /// of the other chips and not the raw loaded total.
        ///
        /// One pass: the status-independent criteria are evaluated once per quest and
        /// only the two cheap status/faction checks run per tag, so the shared work
        /// (three ToLowerInvariant calls per quest under a search) is not repeated once
        /// per chip. <see cref="Matches"/> composes the same three parts, so the counts
        /// are by construction what the list would show.
        /// </summary>
        public static Dictionary<string, int> CountByStatusTag(
            IReadOnlyList<QuestViewModel> viewModels,
            QuestFilterCriteria criteria,
            IReadOnlyList<string> statusTags)
        {
            var counts = new Dictionary<string, int>(statusTags.Count, StringComparer.Ordinal);
            foreach (var tag in statusTags)
                counts[tag] = 0;

            foreach (var vm in viewModels)
            {
                if (!MatchesNonStatusCriteria(vm, criteria))
                    continue;

                foreach (var tag in statusTags)
                {
                    if (MatchesStatusTag(vm, tag) && MatchesFaction(vm, criteria.Faction, tag))
                        counts[tag]++;
                }
            }

            return counts;
        }
    }
}
