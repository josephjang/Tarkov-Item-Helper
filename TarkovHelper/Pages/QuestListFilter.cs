using TarkovHelper.Models;

namespace TarkovHelper.Pages
{
    /// <summary>
    /// The quest-list filter inputs as read from the filter bar controls, using the
    /// control conventions: an empty string means "no filter" for SearchText, Trader,
    /// and Map; StatusTag is a ComboBoxItem Tag ("All", "Active", "Locked", "Done",
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
    /// The quest-list filter predicate, extracted from QuestListPage.ApplyFilters so
    /// the filter semantics are unit-testable without WPF (see QuestListFilterTests).
    /// Keep this the single place that decides whether a quest is visible under the
    /// filter bar.
    /// </summary>
    public static class QuestListFilter
    {
        public static bool Matches(QuestViewModel vm, QuestFilterCriteria criteria)
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

            // Status filter
            if (criteria.StatusTag != "All")
            {
                // "Locked" filter now includes both Locked and LevelLocked
                if (criteria.StatusTag == "Locked")
                {
                    if (vm.Status != QuestStatus.Locked && vm.Status != QuestStatus.LevelLocked)
                        return false;
                }
                else
                {
                    // An unrecognized tag (a future ComboBox typo, or a new caller of
                    // this public predicate) matches nothing rather than throwing
                    // ArgumentException on the UI thread mid-ApplyFilters.
                    if (!Enum.TryParse<QuestStatus>(criteria.StatusTag, out var statusFilter))
                        return false;
                    if (vm.Status != statusFilter)
                        return false;
                }
            }

            // Faction filter - hide quests for the other faction
            // Exception: Show in Unavailable filter so users can see faction-restricted quests
            if (!string.IsNullOrEmpty(criteria.Faction) && !string.IsNullOrEmpty(vm.Task.Faction))
            {
                if (!string.Equals(vm.Task.Faction, criteria.Faction, StringComparison.OrdinalIgnoreCase))
                {
                    // Only hide if NOT viewing Unavailable status
                    if (criteria.StatusTag != "Unavailable")
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// For each status tag, the number of quests that would be visible if the status
        /// filter were set to that tag while every other criterion stays as-is — the
        /// numbers on the quest page's status chips, which are click-previews by
        /// definition (clicking a chip applies exactly the counted filter). Because of
        /// the faction/Unavailable exception in <see cref="Matches"/>, an other-faction
        /// quest is counted only under the "Unavailable" tag, so chip counts need not
        /// sum to any fixed total.
        /// </summary>
        public static Dictionary<string, int> CountByStatusTag(
            IReadOnlyList<QuestViewModel> viewModels,
            QuestFilterCriteria criteria,
            IReadOnlyList<string> statusTags)
        {
            var counts = new Dictionary<string, int>(statusTags.Count, StringComparer.Ordinal);
            foreach (var tag in statusTags)
            {
                // `with` copies the record (including the already-normalized search
                // text backing field) and replaces only the status tag.
                var tagCriteria = criteria with { StatusTag = tag };
                counts[tag] = viewModels.Count(vm => Matches(vm, tagCriteria));
            }
            return counts;
        }
    }
}
