using System.Reflection;
using System.Runtime.CompilerServices;
using TarkovHelper.Models;
using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Unit guards for QuestProgressService.ComputeCompletionCascade — the pure
/// traversal core shared by CompleteQuest and GetCompletionCascade (see
/// feature-quest-complete-cascade-confirm.spec.md). The tests encode the
/// pre-refactor CompleteQuest semantics, including the obscure ones (dual-key
/// done-check, TaskRequirements-over-Previous precedence, planned-write
/// visibility), so the shared-core refactor cannot silently change completion
/// behavior. Also guards the QuestCompleteConfirmDialog localization strings.
/// </summary>
public sealed class QuestCompletionCascadeTests
{
    /// <summary>
    /// Dictionary-backed stand-in for the service's lookups: tasks keyed by Id and
    /// NormalizedName plus a raw recorded-progress map. GetStatus mirrors the
    /// service's recorded-state read order (Id first; NormalizedName only when the
    /// task has no Id) and reports Active otherwise — Done/Failed vs anything else
    /// is the only distinction the cascade gates make.
    /// </summary>
    private sealed class QuestWorld
    {
        private readonly Dictionary<string, TarkovTask> _byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TarkovTask> _byName = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, QuestStatus> Recorded { get; } = new(StringComparer.OrdinalIgnoreCase);

        public TarkovTask Add(string id, string name)
        {
            var task = new TarkovTask
            {
                Ids = string.IsNullOrEmpty(id) ? new List<string>() : new List<string> { id },
                Name = name,
                NormalizedName = name,
                Trader = "Prapor",
            };
            if (!string.IsNullOrEmpty(id)) _byId[id] = task;
            _byName[name] = task;
            return task;
        }

        public QuestStatus GetStatus(TarkovTask task)
        {
            var id = task.Ids?.FirstOrDefault();
            if (!string.IsNullOrEmpty(id))
            {
                if (Recorded.TryGetValue(id, out var byId)) return byId;
            }
            else if (task.NormalizedName != null && Recorded.TryGetValue(task.NormalizedName, out var byName))
            {
                return byName;
            }
            return QuestStatus.Active;
        }

        public (List<TarkovTask> ToComplete, List<(TarkovTask Task, string ListedName)> ToFail) Cascade(
            TarkovTask task, bool completePrerequisites = true)
            => QuestProgressService.ComputeCompletionCascade(
                task, completePrerequisites,
                id => _byId.GetValueOrDefault(id),
                name => _byName.GetValueOrDefault(name),
                GetStatus,
                key => Recorded.TryGetValue(key, out var status) ? status : null);
    }

    private static TaskRequirement RequireById(TarkovTask task)
        => new() { TaskId = task.Ids!.First() };

    private static TaskRequirement RequireByName(TarkovTask task)
        => new() { TaskNormalizedName = task.NormalizedName! };

    [Fact]
    public void Standalone_quest_yields_only_itself_and_no_failures()
    {
        var world = new QuestWorld();
        var quest = world.Add("q1", "standalone");

        var (toComplete, toFail) = world.Cascade(quest);

        Assert.Equal(new[] { quest }, toComplete);
        Assert.Empty(toFail);
    }

    [Fact]
    public void Already_done_prerequisites_are_excluded()
    {
        var world = new QuestWorld();
        var prereq = world.Add("a", "prereq-a");
        var quest = world.Add("b", "quest-b");
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(prereq) };
        world.Recorded["a"] = QuestStatus.Done;

        var (toComplete, toFail) = world.Cascade(quest);

        Assert.Equal(new[] { quest }, toComplete);
        Assert.Empty(toFail);
    }

    [Fact]
    public void Deep_chain_is_traversed_post_order_with_the_clicked_quest_last()
    {
        var world = new QuestWorld();
        var a = world.Add("a", "chain-a");
        var b = world.Add("b", "chain-b");
        var c = world.Add("c", "chain-c");
        // Mixed requirement shapes: by Id and by NormalizedName resolve alike.
        b.TaskRequirements = new List<TaskRequirement> { RequireByName(a) };
        c.TaskRequirements = new List<TaskRequirement> { RequireById(b) };

        var (toComplete, _) = world.Cascade(c);

        Assert.Equal(new[] { a, b, c }, toComplete);
    }

    [Fact]
    public void Diamond_dependency_lists_the_shared_prerequisite_once()
    {
        var world = new QuestWorld();
        var d = world.Add("d", "shared-d");
        var b = world.Add("b", "mid-b");
        var c = world.Add("c", "mid-c");
        var a = world.Add("a", "top-a");
        b.TaskRequirements = new List<TaskRequirement> { RequireById(d) };
        c.TaskRequirements = new List<TaskRequirement> { RequireById(d) };
        a.TaskRequirements = new List<TaskRequirement> { RequireById(b), RequireById(c) };

        var (toComplete, _) = world.Cascade(a);

        Assert.Equal(new[] { d, b, c, a }, toComplete);
    }

    [Fact]
    public void Prerequisite_with_alternatives_is_skipped_along_with_its_subtree()
    {
        var world = new QuestWorld();
        var deep = world.Add("deep", "behind-choice");
        var choice = world.Add("choice", "choice-quest");
        var quest = world.Add("q", "clicked-quest");
        choice.TaskRequirements = new List<TaskRequirement> { RequireById(deep) };
        choice.AlternativeQuests = new List<string> { "some-other-choice" };
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(choice) };

        var (toComplete, toFail) = world.Cascade(quest);

        // The user must pick which mutually exclusive choice to complete, so the
        // cascade completes neither the choice quest nor anything behind it.
        Assert.Equal(new[] { quest }, toComplete);
        Assert.Empty(toFail);
    }

    [Fact]
    public void Alternatives_already_done_or_failed_are_not_failed_again()
    {
        var world = new QuestWorld();
        var quest = world.Add("q", "quest-with-alts");
        var active = world.Add("x", "alt-active");
        var done = world.Add("y", "alt-done");
        var failed = world.Add("z", "alt-failed");
        quest.AlternativeQuests = new List<string> { active.NormalizedName!, done.NormalizedName!, failed.NormalizedName! };
        world.Recorded["y"] = QuestStatus.Done;
        world.Recorded["z"] = QuestStatus.Failed;

        var (_, toFail) = world.Cascade(quest);

        var entry = Assert.Single(toFail);
        Assert.Same(active, entry.Task);
        Assert.Equal("alt-active", entry.ListedName);
    }

    [Fact]
    public void Mutually_requiring_quests_terminate_and_appear_once_each()
    {
        var world = new QuestWorld();
        var a = world.Add("a", "cycle-a");
        var b = world.Add("b", "cycle-b");
        a.TaskRequirements = new List<TaskRequirement> { RequireById(b) };
        b.TaskRequirements = new List<TaskRequirement> { RequireById(a) };

        var (toComplete, _) = world.Cascade(a);

        Assert.Equal(new[] { b, a }, toComplete);
    }

    [Fact]
    public void Empty_but_non_null_TaskRequirements_does_not_fall_back_to_Previous()
    {
        var world = new QuestWorld();
        var legacyPrereq = world.Add("p", "legacy-prereq");
        var quest = world.Add("q", "quest-with-empty-reqs");
        quest.TaskRequirements = new List<TaskRequirement>();
        quest.Previous = new List<string> { legacyPrereq.NormalizedName! };

        var (toComplete, _) = world.Cascade(quest);

        // Pre-refactor behavior: the Previous list is consulted only when
        // TaskRequirements is null, not merely empty.
        Assert.Equal(new[] { quest }, toComplete);
    }

    [Fact]
    public void Previous_list_is_used_when_TaskRequirements_is_null()
    {
        var world = new QuestWorld();
        // No Ids: the legacy shape keys everything by NormalizedName.
        var prereq = world.Add("", "legacy-prereq");
        var quest = world.Add("", "legacy-quest");
        quest.Previous = new List<string> { prereq.NormalizedName! };

        var (toComplete, _) = world.Cascade(quest);

        Assert.Equal(new[] { prereq, quest }, toComplete);
    }

    [Fact]
    public void Prerequisite_recorded_done_under_its_name_only_is_still_excluded()
    {
        var world = new QuestWorld();
        var prereq = world.Add("p1", "migrated-prereq");
        var quest = world.Add("q1", "quest-on-migrated");
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(prereq) };
        // Legacy migration shape: progress recorded under NormalizedName although the
        // task now has an Id. GetStatus misses it (it reads the Id key), but the
        // traversal's node-entry check reads both keys — the quest must not be
        // re-completed.
        world.Recorded["migrated-prereq"] = QuestStatus.Done;

        var (toComplete, _) = world.Cascade(quest);

        Assert.Equal(new[] { quest }, toComplete);
    }

    [Fact]
    public void Alternative_that_the_cascade_itself_completes_is_not_also_failed()
    {
        var world = new QuestWorld();
        var prereq = world.Add("p", "shared-prereq");
        var quest = world.Add("q", "asymmetric-quest");
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(prereq) };
        // Asymmetric data: the quest lists its own prerequisite as an alternative,
        // but the prerequisite lists nothing (so the traversal does not skip it).
        quest.AlternativeQuests = new List<string> { prereq.NormalizedName! };

        var (toComplete, toFail) = world.Cascade(quest);

        // The old interleaved code marked the prerequisite Done before the
        // alternative pass ran, so it was never failed; the planned-set must
        // reproduce that.
        Assert.Equal(new[] { prereq, quest }, toComplete);
        Assert.Empty(toFail);
    }

    [Fact]
    public void Already_done_quest_completes_nothing_but_still_fails_alternatives()
    {
        var world = new QuestWorld();
        var quest = world.Add("q", "done-quest");
        var alt = world.Add("x", "live-alt");
        quest.AlternativeQuests = new List<string> { alt.NormalizedName! };
        world.Recorded["q"] = QuestStatus.Done;

        var (toComplete, toFail) = world.Cascade(quest);

        Assert.Empty(toComplete);
        Assert.Same(alt, Assert.Single(toFail).Task);
    }

    [Fact]
    public void Unresolvable_prerequisites_and_alternatives_are_ignored()
    {
        var world = new QuestWorld();
        var quest = world.Add("q", "quest-with-ghosts");
        quest.TaskRequirements = new List<TaskRequirement> { new() { TaskId = "no-such-id" } };
        quest.AlternativeQuests = new List<string> { "no-such-alt" };

        var (toComplete, toFail) = world.Cascade(quest);

        Assert.Equal(new[] { quest }, toComplete);
        Assert.Empty(toFail);
    }

    [Fact]
    public void Preview_is_pure_and_repeatable()
    {
        var world = new QuestWorld();
        var a = world.Add("a", "pure-a");
        var b = world.Add("b", "pure-b");
        var alt = world.Add("x", "pure-alt");
        b.TaskRequirements = new List<TaskRequirement> { RequireById(a) };
        b.AlternativeQuests = new List<string> { alt.NormalizedName! };
        world.Recorded["seed"] = QuestStatus.Failed;

        var first = world.Cascade(b);
        var second = world.Cascade(b);

        // Nothing recorded changed, and a second run sees the same world.
        Assert.Equal(new[] { ("seed", QuestStatus.Failed) },
            world.Recorded.Select(kv => (kv.Key, kv.Value)));
        Assert.Equal(first.ToComplete, second.ToComplete);
        Assert.Equal(first.ToFail, second.ToFail);
    }

    #region Dialog localization strings

    private static readonly string[] CascadeStringKeys =
    {
        "CascadeConfirmTitle", "CascadeConfirmQuestFormat", "CascadeCompletedHeaderFormat",
        "CascadeFailedHeaderFormat", "CascadeFailedNote", "CascadeConfirmButton",
    };

    private static readonly string[] CascadeFormatKeys =
    {
        "CascadeConfirmQuestFormat", "CascadeCompletedHeaderFormat", "CascadeFailedHeaderFormat",
    };

    /// <summary>
    /// The real constructor opens user_data.db via UserDataDbService; an uninitialized
    /// instance skips that, and the string properties only read _currentLanguage
    /// (same approach as LocalizationHeaderStringsTests).
    /// </summary>
    private static LocalizationService CreateWithoutDb(AppLanguage language)
    {
        var loc = (LocalizationService)RuntimeHelpers.GetUninitializedObject(typeof(LocalizationService));
        var field = typeof(LocalizationService)
            .GetField("_currentLanguage", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(loc, language);
        return loc;
    }

    private static string GetString(LocalizationService loc, string key)
    {
        var prop = typeof(LocalizationService).GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(prop != null, $"LocalizationService has no public property '{key}'");
        return (string)prop!.GetValue(loc)!;
    }

    [Theory]
    [InlineData(AppLanguage.EN)]
    [InlineData(AppLanguage.KO)]
    [InlineData(AppLanguage.JA)]
    public void Every_cascade_dialog_string_is_nonempty(AppLanguage language)
    {
        var loc = CreateWithoutDb(language);
        foreach (var key in CascadeStringKeys)
        {
            Assert.False(string.IsNullOrWhiteSpace(GetString(loc, key)), $"'{key}' is empty for {language}");
        }
    }

    [Theory]
    [InlineData(AppLanguage.EN)]
    [InlineData(AppLanguage.KO)]
    [InlineData(AppLanguage.JA)]
    public void Cascade_format_strings_keep_their_argument_slot(AppLanguage language)
    {
        var loc = CreateWithoutDb(language);
        foreach (var key in CascadeFormatKeys)
        {
            Assert.Contains("{0}", GetString(loc, key));
        }
    }

    [Fact]
    public void Cascade_dialog_strings_are_translated_for_korean_and_japanese()
    {
        var en = CreateWithoutDb(AppLanguage.EN);
        var ko = CreateWithoutDb(AppLanguage.KO);
        var ja = CreateWithoutDb(AppLanguage.JA);

        foreach (var key in CascadeStringKeys)
        {
            Assert.NotEqual(GetString(en, key), GetString(ko, key));
            Assert.NotEqual(GetString(en, key), GetString(ja, key));
        }
    }

    #endregion
}
