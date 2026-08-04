using System.Reflection;
using System.Runtime.CompilerServices;
using TarkovHelper.Models;
using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Unit guards for QuestProgressService.ComputeCompletionCascade — the pure
/// traversal core shared by CompleteQuest and GetCompletionCascade (see
/// feature-quest-complete-cascade-confirm.spec.md). The tests encode the
/// pre-refactor CompleteQuest semantics that were kept (dual-key done-check,
/// TaskRequirements-over-Previous precedence, planned-write visibility) plus
/// the requirement semantics the review corrected: Fail-/Accept-type
/// requirements and multi-member OR groups are never auto-completed (the
/// pre-refactor code over-completed both). Also guards the plan shape the
/// apply step writes verbatim and the QuestCompleteConfirmDialog localization
/// strings.
/// </summary>
public sealed class QuestCompletionCascadeTests
{
    /// <summary>
    /// Dictionary-backed stand-in for the service's lookups: tasks keyed by Id and
    /// NormalizedName plus a raw recorded-progress map.
    /// </summary>
    private sealed class QuestWorld
    {
        private readonly Dictionary<string, TarkovTask> _byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TarkovTask> _byName = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, QuestStatus> Recorded { get; } = new(StringComparer.OrdinalIgnoreCase);

        public TarkovTask Add(string id, string name)
            => AddWithIds(string.IsNullOrEmpty(id) ? new List<string>() : new List<string> { id }, name);

        /// <summary>Registers a task with an explicit Ids list (e.g. to model an empty-string Id anomaly).</summary>
        public TarkovTask AddWithIds(List<string> ids, string name)
        {
            var task = new TarkovTask
            {
                Ids = ids,
                Name = name,
                NormalizedName = name,
                Trader = "Prapor",
            };
            foreach (var id in ids.Where(i => !string.IsNullOrEmpty(i))) _byId[id] = task;
            _byName[name] = task;
            return task;
        }

        /// <summary>
        /// Mirrors the real QuestProgressService.GetStatus recorded-state read order:
        /// the Id key is consulted first, and the NormalizedName key whenever the Id
        /// lookup MISSES — including for tasks that do have an Id. Only a recorded
        /// Done/Failed short-circuits; everything else reports Active, which is the
        /// only distinction the cascade gates make.
        /// </summary>
        public QuestStatus GetStatus(TarkovTask task)
        {
            var id = task.Ids?.FirstOrDefault();
            if (!string.IsNullOrEmpty(id) && Recorded.TryGetValue(id, out var byId))
            {
                if (byId is QuestStatus.Done or QuestStatus.Failed) return byId;
            }
            else if (task.NormalizedName != null && Recorded.TryGetValue(task.NormalizedName, out var byName))
            {
                if (byName is QuestStatus.Done or QuestStatus.Failed) return byName;
            }
            return QuestStatus.Active;
        }

        public QuestCompletionPlan Plan(TarkovTask task, bool completePrerequisites = true)
            => QuestProgressService.ComputeCompletionCascade(
                task, completePrerequisites,
                id => _byId.GetValueOrDefault(id),
                name => _byName.GetValueOrDefault(name),
                GetStatus,
                key => Recorded.TryGetValue(key, out var status) ? status : null);

        public (List<TarkovTask> ToComplete, List<(TarkovTask Task, string Key)> ToFail) Cascade(
            TarkovTask task, bool completePrerequisites = true)
        {
            var plan = Plan(task, completePrerequisites);
            return (plan.CompletionsInOrder.Select(c => c.Quest).ToList(),
                    plan.AlternativesToFail.Select(a => (a.Quest, a.Key)).ToList());
        }
    }

    private static TaskRequirement RequireById(TarkovTask task, string? requirementType = null, int groupId = 0)
        => new()
        {
            TaskId = task.Ids!.First(),
            Status = requirementType == null ? null : new List<string> { requirementType },
            GroupId = groupId,
        };

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
        // The fail key prefers the task's Id; the listed name is only the
        // fallback for id-less legacy tasks (covered separately below).
        Assert.Equal("x", entry.Key);
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
        // Legacy migration shape: an Active row sits under the Id key while the Done
        // row sits under the NormalizedName. GetStatus resolves the Id hit (Active,
        // no short-circuit) and never reads the name key — only the traversal's
        // node-entry check does, and it must see the name-keyed Done row and leave
        // the prerequisite out of the plan.
        world.Recorded["p1"] = QuestStatus.Active;
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
    public void Preview_is_repeatable()
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

        // A second run over the unchanged world plans the same result. (The core
        // cannot mutate the world through its read-only delegates by construction;
        // the instance-level purity guard for GetCompletionCascade lives in
        // GetCompletionCascade_lists_prerequisites_and_mutates_nothing.)
        Assert.Equal(first.ToComplete, second.ToComplete);
        Assert.Equal(first.ToFail, second.ToFail);
    }

    #region Requirement semantics (Status / GroupId — mirrored from ArePrerequisitesMet)

    [Fact]
    public void Fail_type_prerequisite_is_never_auto_completed()
    {
        var world = new QuestWorld();
        var mustFail = world.Add("hw", "must-fail-prereq");
        var quest = world.Add("q", "requires-a-failure");
        // The game requires the prerequisite to be FAILED — completing it for the
        // player would be the exact opposite of the required state.
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(mustFail, "Fail") };

        var (toComplete, toFail) = world.Cascade(quest);

        Assert.Equal(new[] { quest }, toComplete);
        Assert.Empty(toFail);
    }

    [Fact]
    public void Accept_type_prerequisite_is_never_auto_completed()
    {
        var world = new QuestWorld();
        var mustStart = world.Add("p", "must-start-prereq");
        var quest = world.Add("q", "requires-a-start");
        // The prerequisite only needs to be STARTED; marking it fully Done would
        // over-record progress the player never made.
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(mustStart, "Accept") };

        var (toComplete, _) = world.Cascade(quest);

        Assert.Equal(new[] { quest }, toComplete);
    }

    [Fact]
    public void Multi_member_or_group_is_never_auto_completed()
    {
        var world = new QuestWorld();
        var branchA = world.Add("a", "or-branch-a");
        var branchB = world.Add("b", "or-branch-b");
        var quest = world.Add("q", "either-or-quest");
        // Any ONE of the group satisfies the requirement — the user must choose
        // which branch they actually did; completing both over-records.
        quest.TaskRequirements = new List<TaskRequirement>
        {
            RequireById(branchA, "Complete", groupId: 1),
            RequireById(branchB, "Complete", groupId: 1),
        };

        var (toComplete, _) = world.Cascade(quest);

        Assert.Equal(new[] { quest }, toComplete);
    }

    [Fact]
    public void Or_group_subtrees_are_skipped_with_the_group()
    {
        var world = new QuestWorld();
        var deep = world.Add("deep", "behind-or-branch");
        var branchA = world.Add("a", "or-a");
        var branchB = world.Add("b", "or-b");
        var quest = world.Add("q", "or-quest");
        branchA.TaskRequirements = new List<TaskRequirement> { RequireById(deep) };
        quest.TaskRequirements = new List<TaskRequirement>
        {
            RequireById(branchA, groupId: 2),
            RequireById(branchB, groupId: 2),
        };

        var (toComplete, _) = world.Cascade(quest);

        Assert.Equal(new[] { quest }, toComplete);
    }

    [Fact]
    public void Single_member_group_cascades_like_a_plain_requirement()
    {
        var world = new QuestWorld();
        var prereq = world.Add("p", "lone-group-prereq");
        var quest = world.Add("q", "lone-group-quest");
        // A one-member "group" is just a requirement (ArePrerequisitesMet treats it
        // the same); the e2e data queries rely on single-requirement quests cascading.
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(prereq, "Complete", groupId: 1) };

        var (toComplete, _) = world.Cascade(quest);

        Assert.Equal(new[] { prereq, quest }, toComplete);
    }

    [Fact]
    public void Mixed_requirements_cascade_only_the_plain_completable_ones()
    {
        var world = new QuestWorld();
        var plain = world.Add("c", "plain-complete");
        var mustFail = world.Add("f", "fail-type");
        var branchA = world.Add("a", "mix-or-a");
        var branchB = world.Add("b", "mix-or-b");
        var quest = world.Add("q", "mixed-quest");
        quest.TaskRequirements = new List<TaskRequirement>
        {
            RequireById(plain),
            RequireById(mustFail, "Fail"),
            RequireById(branchA, "Complete", groupId: 1),
            RequireById(branchB, "Complete", groupId: 1),
        };

        var (toComplete, _) = world.Cascade(quest);

        Assert.Equal(new[] { plain, quest }, toComplete);
    }

    #endregion

    #region completePrerequisites: false (the log-sync call mode)

    [Fact]
    public void Without_prerequisites_flag_only_the_clicked_quest_completes()
    {
        var world = new QuestWorld();
        var prereq = world.Add("p", "flagged-prereq");
        var quest = world.Add("q", "flagged-quest");
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(prereq) };

        var (toComplete, _) = world.Cascade(quest, completePrerequisites: false);

        Assert.Equal(new[] { quest }, toComplete);
    }

    [Fact]
    public void Without_prerequisites_flag_alternatives_still_fail()
    {
        var world = new QuestWorld();
        var alt = world.Add("x", "flagged-alt");
        var quest = world.Add("q", "flagged-alt-quest");
        quest.AlternativeQuests = new List<string> { alt.NormalizedName! };

        var (toComplete, toFail) = world.Cascade(quest, completePrerequisites: false);

        Assert.Equal(new[] { quest }, toComplete);
        Assert.Same(alt, Assert.Single(toFail).Task);
    }

    #endregion

    #region Plan shape (what ApplyCompletionCascade writes verbatim)

    [Fact]
    public void Plan_reports_the_clicked_quest_separately_from_prerequisites()
    {
        var world = new QuestWorld();
        var prereq = world.Add("a", "plan-prereq");
        var quest = world.Add("b", "plan-quest");
        quest.TaskRequirements = new List<TaskRequirement> { RequireById(prereq) };

        var plan = world.Plan(quest);

        Assert.Equal(new[] { prereq }, plan.Prerequisites.Select(p => p.Quest));
        Assert.NotNull(plan.ClickedQuest);
        Assert.Same(quest, plan.ClickedQuest!.Value.Quest);
        Assert.Equal("b", plan.ClickedQuest.Value.Key);
        Assert.Equal(new[] { prereq, quest }, plan.CompletionsInOrder.Select(c => c.Quest));
    }

    [Fact]
    public void Plan_has_no_clicked_entry_when_the_quest_is_already_done()
    {
        var world = new QuestWorld();
        var quest = world.Add("q", "done-plan-quest");
        var alt = world.Add("x", "done-plan-alt");
        quest.AlternativeQuests = new List<string> { alt.NormalizedName! };
        world.Recorded["q"] = QuestStatus.Done;

        var plan = world.Plan(quest);

        Assert.Null(plan.ClickedQuest);
        Assert.Empty(plan.CompletionsInOrder);
        Assert.Empty(plan.Prerequisites);
        Assert.Same(alt, Assert.Single(plan.AlternativesToFail).Quest);
    }

    [Fact]
    public void Planned_key_falls_back_to_normalized_name_when_the_first_id_is_empty()
    {
        var world = new QuestWorld();
        // Data anomaly: an Ids list whose first entry is the empty string must not
        // become the literal progress key "" (nor abort the completion silently).
        var quest = world.AddWithIds(new List<string> { "" }, "empty-id-quest");

        var plan = world.Plan(quest);

        var completion = Assert.Single(plan.CompletionsInOrder);
        Assert.Equal("empty-id-quest", completion.Key);
    }

    [Fact]
    public void Alternative_fail_key_uses_the_listed_name_for_id_less_tasks()
    {
        var world = new QuestWorld();
        var alt = world.Add("", "legacy-alt");
        var quest = world.Add("q", "legacy-alt-quest");
        quest.AlternativeQuests = new List<string> { alt.NormalizedName! };

        var plan = world.Plan(quest);

        var failure = Assert.Single(plan.AlternativesToFail);
        Assert.Same(alt, failure.Quest);
        Assert.Equal("legacy-alt", failure.Key);
    }

    #endregion

    #region GetCompletionCascade (instance preview + purity)

    /// <summary>
    /// Uninitialized-instance service (same pattern as CreateWithoutDb below): the
    /// private ctor's ProfileService subscription and the DB load are skipped and
    /// the lookup fields are seeded directly, so the public preview entry point —
    /// GetCompletionCascade and QuestCompletionCascade.IsEmpty, the actual
    /// dialog-or-no-dialog decision — is exercised for real.
    /// </summary>
    private static QuestProgressService CreateServiceWith(params TarkovTask[] tasks)
    {
        var service = (QuestProgressService)RuntimeHelpers.GetUninitializedObject(typeof(QuestProgressService));
        void Set(string field, object value)
        {
            var f = typeof(QuestProgressService).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.True(f != null, $"QuestProgressService has no field '{field}'");
            f!.SetValue(service, value);
        }

        var byId = new Dictionary<string, TarkovTask>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, TarkovTask>(StringComparer.OrdinalIgnoreCase);
        foreach (var task in tasks)
        {
            foreach (var id in task.Ids ?? new List<string>())
            {
                if (!string.IsNullOrEmpty(id)) byId[id] = task;
            }
            if (task.NormalizedName != null) byName[task.NormalizedName] = task;
        }

        Set("_questProgress", new Dictionary<string, QuestStatus>(StringComparer.OrdinalIgnoreCase));
        Set("_tasksById", byId);
        Set("_tasksByNormalizedName", byName);
        Set("_tasksByBsgId", new Dictionary<string, TarkovTask>(StringComparer.OrdinalIgnoreCase));
        Set("_allTasks", tasks.ToList());
        return service;
    }

    private static Dictionary<string, QuestStatus> RecordedProgressOf(QuestProgressService service)
        => (Dictionary<string, QuestStatus>)typeof(QuestProgressService)
            .GetField("_questProgress", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(service)!;

    private static TarkovTask NewTask(string id, string name) => new()
    {
        Ids = new List<string> { id },
        Name = name,
        NormalizedName = name,
        Trader = "Prapor",
    };

    [Fact]
    public void GetCompletionCascade_is_empty_for_a_standalone_quest()
    {
        var quest = NewTask("s1", "instance-standalone");
        var service = CreateServiceWith(quest);

        var cascade = service.GetCompletionCascade(quest);

        Assert.True(cascade.IsEmpty);
        Assert.Empty(cascade.PrerequisitesToComplete);
        Assert.Empty(cascade.AlternativesToFail);
    }

    [Fact]
    public void GetCompletionCascade_lists_prerequisites_and_mutates_nothing()
    {
        var prereq = NewTask("p1", "instance-prereq");
        var quest = NewTask("q1", "instance-quest");
        quest.TaskRequirements = new List<TaskRequirement> { new() { TaskId = "p1" } };
        var service = CreateServiceWith(prereq, quest);

        var cascade = service.GetCompletionCascade(quest);

        Assert.False(cascade.IsEmpty);
        // The clicked quest itself is excluded from the preview list.
        Assert.Equal(new[] { prereq }, cascade.PrerequisitesToComplete);
        // The preview is genuinely side-effect-free on recorded progress.
        Assert.Empty(RecordedProgressOf(service));
    }

    #endregion

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
