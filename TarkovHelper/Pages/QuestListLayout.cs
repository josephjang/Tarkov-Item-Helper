namespace TarkovHelper.Pages
{
    /// <summary>
    /// The pure layout decisions of the quest page's splitter panel, kept out of
    /// QuestListPage so they are unit-testable without WPF (see QuestListLayoutTests) —
    /// dragging an 8px transparent GridSplitter with a real mouse is too brittle for UIA,
    /// so this is where the restore/clamp rules are actually pinned.
    /// </summary>
    public static class QuestListLayout
    {
        /// <summary>
        /// The width to give the detail column: the requested (persisted or dragged)
        /// width held inside the settings bounds, and additionally capped to what the
        /// page can show once the quest list keeps its minimum width and the splitter
        /// its own.
        ///
        /// The cap matters because the persisted width travels between windows: a panel
        /// sized on a maximized ultrawide would otherwise push the list and the splitter
        /// past the right edge of a narrow window, where the user can no longer grab the
        /// splitter to drag it back. It is applied only while it still leaves at least
        /// <paramref name="minWidth"/> — on a window too narrow for both, the minimum
        /// wins and WPF clips, rather than collapsing the panel to nothing.
        ///
        /// <paramref name="pageWidth"/> is 0 before the first layout pass; that yields a
        /// negative available width, so the cap is skipped and the bounded request stands.
        /// </summary>
        public static double ClampDetailPanelWidth(
            double requestedWidth,
            double pageWidth,
            double listMinWidth,
            double splitterWidth,
            double minWidth,
            double maxWidth)
        {
            var width = Math.Clamp(requestedWidth, minWidth, maxWidth);

            if (pageWidth <= 0)
            {
                return width;
            }

            // Never below minWidth: on a window too narrow for both columns the panel
            // stays usable at its minimum (leaving the list as much room as possible)
            // instead of collapsing toward zero.
            var available = Math.Max(pageWidth - listMinWidth - splitterWidth, minWidth);
            return Math.Min(width, available);
        }
    }
}
