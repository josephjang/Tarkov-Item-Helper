namespace TarkovHelper.Tests;

/// <summary>
/// End-to-end coverage for feature-quest-overview-filters: the clickable status
/// chips on the statistics bar, the zero-results empty state with its reset button,
/// and quest-tab filter persistence across an app relaunch against the same Config
/// dir.
///
/// PnlEmptyState is a StackPanel with no UI Automation peer, so its Button
/// (BtnResetFilters) is the probe for "empty state visible" — the same pattern
/// QuestNavigationE2ETests uses for PnlFilteredOutNotice/BtnShowInList.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class QuestOverviewFiltersE2ETests : E2ETestBase
{
    /// <summary>Polls until CmbStatus reports the expected selected item name.</summary>
    private static void WaitForStatus(AppDriver app, string expected)
        => WaitUntil(() => app.WaitForComboSelection("CmbStatus") == expected,
            $"status combo to show '{expected}'");

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
        WaitForStatus(app, "All");
    }

    [E2EFact]
    public void Status_chip_applies_that_status_and_clicking_again_returns_to_all()
    {
        using var app = LaunchMaximized();
        app.SelectTab("TabQuests", "LstQuests");
        WaitForStatus(app, "Active"); // the page default, so the chip toggle is deterministic

        app.InvokeElement("ChipDone");
        WaitForStatus(app, "Done");

        app.InvokeElement("ChipDone");
        WaitForStatus(app, "All");
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
        // A tag no build knows: written by a newer version the user rolled back from, or
        // a hand-edited row. The fallback must widen to "All", never narrow to the
        // "Active" default that happens to sit at the combo's index 0.
        E2EDb.SeedSetting(configDir, "questList.statusTag", "NotAStatus");

        using var app = AppDriver.Launch(configDir);
        app.ShowWindow(Win32.SW_MAXIMIZE);
        app.SelectTab("TabQuests", "LstQuests");

        WaitForStatus(app, "All");
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

            app.InvokeElement("ChipDone");     // status filter -> Done
            WaitForStatus(app, "Done");
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

            WaitForStatus(app, "Done");
            Assert.True(app.GetToggleState("ChkKappaOnly"),
                "the Kappa checkbox should be restored checked after a relaunch");
            // Search text is deliberately transient — always empty on a fresh launch.
            Assert.Equal("", app.GetTextBoxValue("TxtSearch"));

            app.CloseAndWaitForExit();
        }
    }
}
