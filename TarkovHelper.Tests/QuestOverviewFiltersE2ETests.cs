namespace TarkovHelper.Tests;

/// <summary>
/// End-to-end coverage for feature-quest-overview-filters and its
/// feature-quest-chip-only-status-filter successor: the status chips as the sole
/// status filter (with the All chip and toggle-to-All), the zero-results empty state
/// with its reset button, and quest-tab filter persistence across an app relaunch
/// against the same Config dir.
///
/// Status-filter state is read through the chips' UIA ItemStatus
/// ("Selected"/"Unselected", published by UpdateStatusChips) via the shared
/// E2ETestBase.WaitForSelectedStatusChip / SelectStatusChip helpers.
///
/// PnlEmptyState is a StackPanel with no UI Automation peer, so its Button
/// (BtnResetFilters) is the probe for "empty state visible" — the same pattern
/// QuestNavigationE2ETests uses for PnlFilteredOutNotice/BtnShowInList.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class QuestOverviewFiltersE2ETests : E2ETestBase
{
    [E2EFact]
    public void Empty_state_appears_at_zero_results_and_reset_restores_the_list()
    {
        using var app = LaunchMaximized();
        app.SelectTab("TabQuests", "LstQuests");

        // No quest name contains this, so the (debounced) search filters to zero rows.
        app.SetTextBoxValue("TxtSearch", "e2e-no-such-quest");
        app.WaitForElementVisibility("BtnResetFilters", visible: true);
        WaitUntil(() => app.GetListItemCount("LstQuests") == 0, "quest list to become empty");

        app.InvokeElement("BtnResetFilters");
        app.WaitForElementVisibility("BtnResetFilters", visible: false);
        WaitUntil(() => app.GetListItemCount("LstQuests") > 0, "quest list to repopulate");
        Assert.Equal("", app.GetTextBoxValue("TxtSearch"));
        // ResetFilters lands on the most-permissive "All", not the "Active" default.
        WaitForSelectedStatusChip(app, "All");
    }

    [E2EFact]
    public void Status_chips_are_the_sole_status_filter_with_All_first_and_toggle_to_All()
    {
        using var app = LaunchMaximized();
        app.SelectTab("TabQuests", "LstQuests");
        WaitForSelectedStatusChip(app, "Active"); // the fresh-profile default, so the flows below are deterministic

        // Clicking a status chip applies that status; the previous chip deselects.
        app.InvokeElement(StatusChipId("Done"));
        WaitForSelectedStatusChip(app, "Done");
        Assert.Equal("Unselected", app.GetItemStatus(StatusChipId("Active")));

        // The All chip is the new direct gesture back to the unfiltered list.
        app.InvokeElement(StatusChipId("All"));
        WaitForSelectedStatusChip(app, "All");

        // Re-clicking the selected status chip still toggles back to All.
        app.InvokeElement(StatusChipId("Done"));
        WaitForSelectedStatusChip(app, "Done");
        app.InvokeElement(StatusChipId("Done"));
        WaitForSelectedStatusChip(app, "All");

        // Regression pins for the combo/stats removal and the chip relabel: the
        // status ComboBox and the "Lv.X | n/m" stats text are gone; the All chip
        // carries a count, and the Unavailable chip says "Unavailable", not "N/A".
        Assert.False(app.IsElementVisible("CmbStatus"),
            "the status ComboBox should be removed — the chips are the only status filter");
        Assert.False(app.IsElementVisible("TxtStats"),
            "the stats text should be removed — its counts live on the chips now");
        Assert.Matches(@"^All \d+$", app.GetElementText(StatusChipId("All")));
        Assert.Matches(@"^Unavailable \d+$", app.GetElementText(StatusChipId("Unavailable")));
    }

    [E2EFact]
    public void A_search_typed_just_before_leaving_the_tab_is_still_applied_on_return()
    {
        using var app = LaunchMaximized();
        app.SelectTab("TabQuests", "LstQuests");

        // Type and leave immediately: the 250ms debounce has not ticked yet. Unloading
        // must FLUSH that pending apply, not cancel it — a cancelled tick would leave
        // the list showing every quest while the search box still reads the query, and
        // Loaded early-returns on the way back so nothing would ever reconcile them.
        app.SetTextBoxValue("TxtSearch", "e2e-no-such-quest");
        app.SelectTab("TabItems", "LstItems", bounceTabAutomationId: "TabQuests");
        app.SelectTab("TabQuests", "LstQuests");

        Assert.Equal("e2e-no-such-quest", app.GetTextBoxValue("TxtSearch"));
        WaitUntil(() => app.GetListItemCount("LstQuests") == 0,
            "the quest list to agree with the search box after the tab round-trip");
        app.WaitForElementVisibility("BtnResetFilters", visible: true);
    }

    [E2EFact]
    public void An_unknown_persisted_status_tag_falls_back_to_All_not_to_Active()
    {
        var configDir = NewConfigDir();
        E2EDb.CreateUserDataDb(configDir);
        // A tag no build knows: written by a newer version the user rolled back from,
        // or a hand-edited row. The restore-time IsKnown validation must widen it to
        // "All", never narrow it to the "Active" fresh-install default.
        E2EDb.SeedSetting(configDir, "questList.statusTag", "NotAStatus");

        using var app = AppDriver.Launch(configDir);
        app.ShowWindow(Win32.SW_MAXIMIZE);
        app.SelectTab("TabQuests", "LstQuests");

        WaitForSelectedStatusChip(app, "All");
        app.CloseAndWaitForExit();
    }

    [E2EFact]
    public void Filter_state_persists_across_an_app_relaunch()
    {
        var configDir = NewConfigDir();

        using (var app = AppDriver.Launch(configDir))
        {
            app.ShowWindow(Win32.SW_MAXIMIZE);
            app.SelectTab("TabQuests", "LstQuests");

            SelectStatusChip(app, "Done");     // status filter -> Done
            app.ToggleElement("ChkKappaOnly"); // Kappa filter -> on
            WaitUntil(() => app.GetToggleState("ChkKappaOnly"), "Kappa checkbox to check");
            app.SetTextBoxValue("TxtSearch", "transient text"); // must NOT survive the relaunch

            app.CloseAndWaitForExit();
        }

        // The snapshot ApplyFilters persisted is readable straight from user_data.db.
        Assert.Equal("Done", E2EDb.ReadSetting(configDir, "questList.statusTag"));
        Assert.Equal("True", E2EDb.ReadSetting(configDir, "questList.kappaOnly"));

        using (var app = AppDriver.Launch(configDir))
        {
            app.ShowWindow(Win32.SW_MAXIMIZE);
            app.SelectTab("TabQuests", "LstQuests");

            // Assert-only: SelectStatusChip's click path could toggle the restored
            // chip — the very state under test — so only wait for it.
            WaitForSelectedStatusChip(app, "Done");
            Assert.True(app.GetToggleState("ChkKappaOnly"),
                "the Kappa checkbox should be restored checked after a relaunch");
            // Search text is deliberately transient — always empty on a fresh launch.
            Assert.Equal("", app.GetTextBoxValue("TxtSearch"));

            app.CloseAndWaitForExit();
        }
    }
}
