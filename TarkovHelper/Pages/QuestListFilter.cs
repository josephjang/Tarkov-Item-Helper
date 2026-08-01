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
        string? Faction);

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
            var searchText = criteria.SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
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
                    var statusFilter = Enum.Parse<QuestStatus>(criteria.StatusTag);
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
    }
}
