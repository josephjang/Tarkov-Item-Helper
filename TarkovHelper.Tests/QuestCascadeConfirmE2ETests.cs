using System.Windows.Automation;

namespace TarkovHelper.Tests;

/// <summary>
/// End-to-end coverage for the quest complete-cascade confirmation dialog (see
/// feature-quest-complete-cascade-confirm.md): completing a quest whose cascade is
/// non-empty must show QuestCompleteConfirmDialog first — Cancel changes nothing,
/// Confirm applies the quest plus its cascade — while a cascade-free completion
/// stays one-click with no dialog.
///
/// The dialog is an owned top-level window, not a reliable UIA descendant of the
/// main window, so it is located by its window title (AppDriver.WaitForAppWindow)
/// and probed with scope-rooted searches. It is opened via InvokePattern
/// (InvokeElement): WPF's ButtonAutomationPeer raises the click asynchronously
/// (Dispatcher.BeginInvoke), so the invoke returns before the handler enters the
/// modal ShowDialog pump — unlike a real mouse click, it cannot miss on a layout
/// shift or a denied SetForegroundWindow.
///
/// Test data comes from E2EQuestData on a fresh profile: the locked-quest query
/// guarantees exactly one Active prerequisite and no OptionalQuests involvement,
/// so the dialog previews exactly one completion and zero failures.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class QuestCascadeConfirmE2ETests : E2ETestBase
{
    /// <summary>
    /// QuestCompleteConfirmDialog's window title. E2e runs use a fresh profile whose
    /// language defaults to EN, so the localized Title the dialog sets matches this.
    /// </summary>
    private const string DialogTitle = "Confirm Quest Completion";

    /// <summary>Test-local waits reuse the harness's shared poll loop (AppDriver.PollUntil).</summary>
    private static void WaitUntil(Func<bool> condition, string what, int timeoutSeconds = 30)
        => AppDriver.PollUntil(condition, what, timeoutSeconds);

    /// <summary>Opens the Quests tab, searches the quest (unique substring), selects it, waits for its detail.</summary>
    private static void ShowQuestDetail(AppDriver app, string questName)
    {
        app.SelectTab("TabQuests", "LstQuests");
        // The status filter defaults to Active on a fresh profile, which would hide
        // the Locked quest under test — show every status before searching.
        app.SelectComboItemByName("CmbStatus", "All");
        app.SetTextBoxValue("TxtSearch", questName);
        // The search filter is debounced (QuestListPage.TxtSearch_TextChanged), so wait
        // for it to apply before touching row 0 — otherwise this could grab the first
        // row of the still-unfiltered list. Both E2EQuestData queries guarantee the
        // name is a unique search substring, so exactly one row survives.
        WaitUntil(() => app.GetListItemCount("LstQuests") == 1,
            $"quest list to filter down to '{questName}'");
        app.SelectListItemAt("LstQuests", 0);
        WaitUntil(() => app.GetElementText("TxtDetailName") == questName,
            $"detail panel to show '{questName}'");
    }

    /// <summary>Invokes Mark Complete and waits for the cascade dialog window.</summary>
    private static AutomationElement OpenCascadeDialog(AppDriver app)
    {
        app.InvokeElement("BtnComplete");
        return app.WaitForAppWindow(DialogTitle);
    }

    /// <summary>
    /// Asserts the dialog previews exactly the one guaranteed prerequisite: the
    /// completed-section header counts 1, the prerequisite is listed, and the failed
    /// section (no alternatives by construction) is absent or collapsed.
    /// </summary>
    private static void AssertDialogPreviewsPrereq(AutomationElement dialog, string prereqName)
    {
        WaitUntil(() => AppDriver.HasTextElementUnder(dialog, prereqName),
            $"cascade dialog to list prerequisite '{prereqName}'");
        Assert.Equal("Will also be completed (1)",
            AppDriver.WaitForElementUnder(dialog, "TxtCascadeCompletedHeader").Current.Name);

        var failedHeader = dialog.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "TxtCascadeFailedHeader"));
        Assert.True(failedHeader == null || failedHeader.Current.IsOffscreen,
            "failed section is visible for a quest without alternatives");
    }

    [E2EFact]
    public void Locked_quest_completion_shows_dialog_and_cancel_changes_nothing()
    {
        var (questName, prereqName) = E2EQuestData.FindLockedQuestWithActivePrereq();
        using var app = LaunchMaximized();

        ShowQuestDetail(app, questName);
        var dialog = OpenCascadeDialog(app);
        AssertDialogPreviewsPrereq(dialog, prereqName);

        AppDriver.Invoke(AppDriver.WaitForElementUnder(dialog, "BtnCascadeCancel"));
        app.WaitForAppWindowClosed(DialogTitle);

        // Nothing changed on the quest: still completable, no Reset button.
        app.WaitForElementVisibility("BtnComplete", visible: true);
        Assert.False(app.IsElementVisible("BtnReset"), "quest gained a Reset button after Cancel");

        // The prerequisite is untouched too — its detail still offers Mark Complete.
        app.ClickTextElementWithScroll(prereqName, "PrerequisitesList", "DetailScrollViewer");
        WaitUntil(() => app.GetElementText("TxtDetailName") == prereqName,
            $"detail panel to show prerequisite '{prereqName}'");
        app.WaitForElementVisibility("BtnComplete", visible: true);
        Assert.False(app.IsElementVisible("BtnReset"), "prerequisite gained a Reset button after Cancel");
    }

    [E2EFact]
    public void Confirm_completes_the_quest_and_its_prerequisite()
    {
        var (questName, prereqName) = E2EQuestData.FindLockedQuestWithActivePrereq();
        using var app = LaunchMaximized();

        ShowQuestDetail(app, questName);
        var dialog = OpenCascadeDialog(app);
        AssertDialogPreviewsPrereq(dialog, prereqName);

        AppDriver.Invoke(AppDriver.WaitForElementUnder(dialog, "BtnCascadeConfirm"));
        app.WaitForAppWindowClosed(DialogTitle);

        // The quest completed: the progress refresh flips its button row.
        app.WaitForElementVisibility("BtnComplete", visible: false);
        app.WaitForElementVisibility("BtnReset", visible: true);

        // The prerequisite was auto-completed with it (its detail offers Reset only).
        app.ClickTextElementWithScroll(prereqName, "PrerequisitesList", "DetailScrollViewer");
        WaitUntil(() => app.GetElementText("TxtDetailName") == prereqName,
            $"detail panel to show prerequisite '{prereqName}'");
        app.WaitForElementVisibility("BtnComplete", visible: false);
        app.WaitForElementVisibility("BtnReset", visible: true);
    }

    [E2EFact]
    public void Cascade_free_completion_shows_no_dialog_and_completes_immediately()
    {
        var questName = E2EQuestData.FindStandaloneActiveQuest();
        using var app = LaunchMaximized();

        ShowQuestDetail(app, questName);
        app.InvokeElement("BtnComplete");

        // One-click completion: the button row flips, and because the click handler
        // decides dialog-or-complete synchronously, a flipped row proves no dialog
        // was (or still is) in the way.
        app.WaitForElementVisibility("BtnComplete", visible: false);
        app.WaitForElementVisibility("BtnReset", visible: true);
        Assert.False(app.HasAppWindow(DialogTitle),
            "cascade dialog appeared for a cascade-free completion");
    }
}
