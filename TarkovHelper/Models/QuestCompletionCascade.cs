namespace TarkovHelper.Models;

/// <summary>
/// Side-effect-free preview of what completing a quest would change beyond the
/// clicked quest itself: the incomplete prerequisites it would auto-complete and
/// the mutually exclusive alternatives it would auto-fail. Produced by
/// <c>QuestProgressService.GetCompletionCascade</c> and applied verbatim by
/// <c>QuestProgressService.ApplyCompletionCascade</c> — the quests the
/// confirmation dialog lists are exactly the quests whose progress changes,
/// because the underlying <see cref="Plan"/> is written as-is, never recomputed.
/// </summary>
public sealed class QuestCompletionCascade
{
    internal QuestCompletionCascade(QuestCompletionPlan plan)
    {
        Plan = plan;
        PrerequisitesToComplete = plan.Prerequisites.Select(p => p.Quest).ToList();
        AlternativesToFail = plan.AlternativesToFail.Select(a => a.Quest).ToList();
    }

    /// <summary>
    /// The full ordered plan this preview projects (including the clicked quest's
    /// own completion, which the preview lists exclude). The apply step writes it
    /// verbatim so preview and apply cannot diverge — not even across the time the
    /// confirmation dialog stays open.
    /// </summary>
    internal QuestCompletionPlan Plan { get; }

    /// <summary>
    /// Prerequisites that would be newly marked Done, in dependency order (deepest
    /// first). Excludes the clicked quest itself.
    /// </summary>
    public IReadOnlyList<TarkovTask> PrerequisitesToComplete { get; }

    /// <summary>Mutually exclusive alternatives that would be marked Failed.</summary>
    public IReadOnlyList<TarkovTask> AlternativesToFail { get; }

    /// <summary>True when completing the quest changes nothing but the quest itself.</summary>
    public bool IsEmpty => PrerequisitesToComplete.Count == 0 && AlternativesToFail.Count == 0;
}

/// <summary>
/// One planned progress write: the quest and the key its status is recorded
/// under. The key is computed once, inside the traversal that proved it usable
/// (first non-empty Id, else NormalizedName; for a failed alternative that has
/// no Id, the raw <c>AlternativeQuests</c> entry it was resolved from — the
/// legacy fallback key the pre-refactor code wrote).
/// </summary>
internal readonly record struct PlannedQuestChange(TarkovTask Quest, string Key);

/// <summary>
/// Ordered, side-effect-free completion plan computed by
/// <c>QuestProgressService.ComputeCompletionCascade</c>: every quest the
/// completion would newly mark Done (post-order — prerequisites before
/// dependents, the clicked quest last when it is planned at all) plus every
/// alternative it would mark Failed.
/// </summary>
internal sealed class QuestCompletionPlan
{
    public QuestCompletionPlan(
        IReadOnlyList<PlannedQuestChange> completionsInOrder,
        PlannedQuestChange? clickedQuest,
        IReadOnlyList<PlannedQuestChange> alternativesToFail)
    {
        CompletionsInOrder = completionsInOrder;
        ClickedQuest = clickedQuest;
        AlternativesToFail = alternativesToFail;
        Prerequisites = clickedQuest is null
            ? completionsInOrder
            : completionsInOrder.Take(completionsInOrder.Count - 1).ToList();
    }

    /// <summary>
    /// Every quest to mark Done, in the exact order the apply step writes them
    /// (post-order; the clicked quest last when planned).
    /// </summary>
    public IReadOnlyList<PlannedQuestChange> CompletionsInOrder { get; }

    /// <summary>
    /// The clicked quest's own planned completion — always the last entry of
    /// <see cref="CompletionsInOrder"/> — or null when the clicked quest is not
    /// planned (already recorded Done, or it carries no usable progress key, in
    /// which case nothing is planned at all).
    /// </summary>
    public PlannedQuestChange? ClickedQuest { get; }

    /// <summary>
    /// <see cref="CompletionsInOrder"/> minus the clicked quest: what the
    /// confirmation dialog previews as "will also be completed".
    /// </summary>
    public IReadOnlyList<PlannedQuestChange> Prerequisites { get; }

    /// <summary>Alternatives to mark Failed, in <c>AlternativeQuests</c> order.</summary>
    public IReadOnlyList<PlannedQuestChange> AlternativesToFail { get; }
}
