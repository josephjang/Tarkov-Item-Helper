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
    /// <summary>Test-local waits reuse the harness's shared poll loop (AppDriver.PollUntil).</summary>
    private static void WaitUntil(Func<bool> condition, string what, int timeoutSeconds = 30)
        => AppDriver.PollUntil(condition, what, timeoutSeconds);

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
