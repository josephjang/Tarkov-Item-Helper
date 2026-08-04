using System.Windows;
using TarkovHelper.Models;
using TarkovHelper.Services;

namespace TarkovHelper.Windows;

/// <summary>
/// Confirmation dialog for quest completions whose cascade is non-empty: previews
/// the prerequisites that will be auto-completed and the mutually exclusive
/// alternatives that will be auto-failed (see
/// QuestProgressService.GetCompletionCascade). Shown by QuestListPage before it
/// calls CompleteQuest; a plain completion (empty cascade) never shows it.
/// See feature-quest-complete-cascade-confirm.md.
/// </summary>
public partial class QuestCompleteConfirmDialog : Window
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    /// <summary>
    /// True only when the user clicked Confirm; Cancel, the X button, Escape, and
    /// any other close path leave it false, so closing the dialog never applies
    /// anything.
    /// </summary>
    public bool Confirmed { get; private set; }

    /// <summary>
    /// Shows the confirmation modal (the construct/Owner/ShowDialog/read-result
    /// protocol lives here, matching the other Windows/ dialogs' static factories)
    /// and returns whether the user confirmed applying <paramref name="cascade"/>.
    /// </summary>
    public static bool Confirm(Window? owner, TarkovTask task, QuestCompletionCascade cascade)
    {
        var dialog = new QuestCompleteConfirmDialog(task, cascade) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Confirmed;
    }

    private QuestCompleteConfirmDialog(TarkovTask task, QuestCompletionCascade cascade)
    {
        InitializeComponent();

        CascadeCompletedList.ItemsSource = cascade.PrerequisitesToComplete.Select(CreateRow).ToList();
        CascadeFailedList.ItemsSource = cascade.AlternativesToFail.Select(CreateRow).ToList();
        CascadeCompletedSection.Visibility =
            cascade.PrerequisitesToComplete.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CascadeFailedSection.Visibility =
            cascade.AlternativesToFail.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        ApplyLocalizedText(task, cascade);
    }

    /// <summary>Row view model for both preview lists: localized quest name + trader.</summary>
    public sealed class CascadeRowViewModel
    {
        public string QuestName { get; init; } = string.Empty;
        public string Trader { get; init; } = string.Empty;
    }

    private CascadeRowViewModel CreateRow(TarkovTask quest) => new()
    {
        QuestName = _loc.GetQuestName(quest),
        Trader = quest.Trader
    };

    private void ApplyLocalizedText(TarkovTask task, QuestCompletionCascade cascade)
    {
        // Title doubles as the Win32 window title the e2e harness locates the
        // (owned, non-descendant) dialog window by.
        Title = _loc.CascadeConfirmTitle;
        TxtCascadeTitle.Text = _loc.CascadeConfirmTitle;
        TxtCascadeQuest.Text = string.Format(_loc.CascadeConfirmQuestFormat, _loc.GetQuestName(task));
        TxtCascadeCompletedHeader.Text =
            string.Format(_loc.CascadeCompletedHeaderFormat, cascade.PrerequisitesToComplete.Count);
        TxtCascadeFailedHeader.Text =
            string.Format(_loc.CascadeFailedHeaderFormat, cascade.AlternativesToFail.Count);
        TxtCascadeFailedNote.Text = _loc.CascadeFailedNote;
        BtnCascadeCancel.Content = _loc.Cancel;
        BtnCascadeConfirm.Content = _loc.CascadeConfirmButton;
    }

    /// <summary>Shared dismiss path: the X button, Cancel, and Escape (via IsCancel).</summary>
    private void BtnCascadeDismiss_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnCascadeConfirm_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }
}
