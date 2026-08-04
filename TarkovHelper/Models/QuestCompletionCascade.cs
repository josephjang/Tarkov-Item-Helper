namespace TarkovHelper.Models;

/// <summary>
/// Side-effect-free preview of what <c>QuestProgressService.CompleteQuest</c> would
/// change beyond the clicked quest itself: the incomplete prerequisites it would
/// auto-complete and the mutually exclusive alternatives it would auto-fail.
/// Produced by <c>QuestProgressService.GetCompletionCascade</c>, which runs the same
/// traversal core the completion applies, so this preview cannot drift from it.
/// </summary>
public sealed class QuestCompletionCascade
{
    public QuestCompletionCascade(
        IReadOnlyList<TarkovTask> prerequisitesToComplete,
        IReadOnlyList<TarkovTask> alternativesToFail)
    {
        PrerequisitesToComplete = prerequisitesToComplete;
        AlternativesToFail = alternativesToFail;
    }

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
