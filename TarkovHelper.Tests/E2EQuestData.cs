using System.IO;
using Microsoft.Data.Sqlite;

namespace TarkovHelper.Tests;

/// <summary>
/// Quest test data derived from the bundled tarkov_data.db (copied to the test
/// output) instead of hard-coded quest names, so the e2e tests survive database
/// updates. Shared by QuestNavigationE2ETests and QuestCascadeConfirmE2ETests.
/// All queries assume a fresh profile: no progress, default player level.
/// </summary>
internal static class E2EQuestData
{
    private static string AssetDbPath => Path.Combine(AppContext.BaseDirectory, "tarkov_data.db");

    /// <summary>
    /// A quest that is Locked on a fresh profile (exactly one Complete-type
    /// prerequisite, nothing else gating it) whose prerequisite is Active on a fresh
    /// profile (no prerequisites of its own, no level/karma/edition/faction gate).
    /// The quest's English name must be unique as a search substring across all
    /// quest names so searching it filters the list down to that single quest.
    ///
    /// Neither the quest nor the prerequisite may appear in OptionalQuests (as
    /// QuestId or AlternativeQuestId): completing the PREREQUISITE must have an
    /// empty cascade so no quest-complete confirmation dialog appears (relied on by
    /// QuestNavigationE2ETests.Detail_buttons_act_on_the_shown_quest_while_it_is_hidden_by_filters),
    /// and completing the QUEST must cascade exactly one completion and zero
    /// failures (relied on by QuestCascadeConfirmE2ETests).
    /// </summary>
    public static (string QuestName, string PrereqName) FindLockedQuestWithActivePrereq()
    {
        using var connection = new SqliteConnection($"Data Source={AssetDbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT q.Name, p.Name
            FROM Quests q
            JOIN QuestRequirements r ON r.QuestId = q.Id
            JOIN Quests p ON p.Id = r.RequiredQuestId
            WHERE r.RequirementType = 'Complete'
              AND (SELECT COUNT(*) FROM QuestRequirements r2 WHERE r2.QuestId = q.Id) = 1
              AND q.Faction IS NULL AND q.RequiredEdition IS NULL
              AND (q.RequiredPrestigeLevel IS NULL OR q.RequiredPrestigeLevel = 0)
              AND (q.RequiredDecodeCount IS NULL OR q.RequiredDecodeCount = 0)
              AND p.Faction IS NULL AND p.RequiredEdition IS NULL
              AND (p.RequiredPrestigeLevel IS NULL OR p.RequiredPrestigeLevel = 0)
              AND (p.RequiredDecodeCount IS NULL OR p.RequiredDecodeCount = 0)
              AND (p.MinLevel IS NULL OR p.MinLevel <= 15)
              AND p.MinScavKarma IS NULL
              AND NOT EXISTS (SELECT 1 FROM QuestRequirements pr WHERE pr.QuestId = p.Id)
              AND NOT EXISTS (SELECT 1 FROM OptionalQuests o
                              WHERE o.QuestId = q.Id OR o.AlternativeQuestId = q.Id)
              AND NOT EXISTS (SELECT 1 FROM OptionalQuests o
                              WHERE o.QuestId = p.Id OR o.AlternativeQuestId = p.Id)
              AND p.Name <> q.Name
              AND (SELECT COUNT(*) FROM Quests q2
                   WHERE instr(lower(q2.Name), lower(q.Name)) > 0
                      OR instr(lower(ifnull(q2.NameKO, '')), lower(q.Name)) > 0
                      OR instr(lower(ifnull(q2.NameJA, '')), lower(q.Name)) > 0) = 1
            ORDER BY q.Name
            LIMIT 1";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(),
            "tarkov_data.db has no locked quest with a single active prerequisite matching the test constraints");
        return (reader.GetString(0), reader.GetString(1));
    }

    /// <summary>
    /// A quest that is Active on a fresh profile with nothing to cascade: no
    /// prerequisites at all and no OptionalQuests involvement, so completing it must
    /// NOT show the confirmation dialog. Its English name must be unique as a search
    /// substring, like <see cref="FindLockedQuestWithActivePrereq"/>.
    /// </summary>
    public static string FindStandaloneActiveQuest()
    {
        using var connection = new SqliteConnection($"Data Source={AssetDbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT q.Name
            FROM Quests q
            WHERE NOT EXISTS (SELECT 1 FROM QuestRequirements r WHERE r.QuestId = q.Id)
              AND NOT EXISTS (SELECT 1 FROM OptionalQuests o
                              WHERE o.QuestId = q.Id OR o.AlternativeQuestId = q.Id)
              AND q.Faction IS NULL AND q.RequiredEdition IS NULL
              AND (q.RequiredPrestigeLevel IS NULL OR q.RequiredPrestigeLevel = 0)
              AND (q.RequiredDecodeCount IS NULL OR q.RequiredDecodeCount = 0)
              AND (q.MinLevel IS NULL OR q.MinLevel <= 15)
              AND q.MinScavKarma IS NULL
              AND (SELECT COUNT(*) FROM Quests q2
                   WHERE instr(lower(q2.Name), lower(q.Name)) > 0
                      OR instr(lower(ifnull(q2.NameKO, '')), lower(q.Name)) > 0
                      OR instr(lower(ifnull(q2.NameJA, '')), lower(q.Name)) > 0) = 1
            ORDER BY q.Name
            LIMIT 1";

        var name = command.ExecuteScalar() as string;
        Assert.False(string.IsNullOrEmpty(name),
            "tarkov_data.db has no prerequisite-free, alternative-free quest matching the test constraints");
        return name!;
    }

    /// <summary>
    /// A quest with exactly ONE mutually exclusive alternative (a single
    /// OptionalQuests row) and no availability gate, whose alternative is a
    /// different quest it does not directly require: completing it must preview
    /// exactly one auto-failed alternative ("Will be FAILED (1)") regardless of the
    /// length of its prerequisite chain, and Confirm must persist the alternative
    /// as Failed. The English name is a unique search substring like the other
    /// queries. Also returns both quest Ids — the keys QuestProgress rows are
    /// written under — for persistence assertions.
    /// </summary>
    public static (string QuestName, string AltName, string QuestId, string AltId) FindQuestWithSingleAlternative()
    {
        using var connection = new SqliteConnection($"Data Source={AssetDbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT q.Name, alt.Name, q.Id, alt.Id
            FROM Quests q
            JOIN OptionalQuests o ON o.QuestId = q.Id
            JOIN Quests alt ON alt.Id = o.AlternativeQuestId
            WHERE (SELECT COUNT(*) FROM OptionalQuests o2 WHERE o2.QuestId = q.Id) = 1
              AND q.Faction IS NULL AND q.RequiredEdition IS NULL
              AND (q.RequiredPrestigeLevel IS NULL OR q.RequiredPrestigeLevel = 0)
              AND (q.RequiredDecodeCount IS NULL OR q.RequiredDecodeCount = 0)
              AND alt.Name <> q.Name
              AND NOT EXISTS (SELECT 1 FROM QuestRequirements qr
                              WHERE qr.QuestId = q.Id AND qr.RequiredQuestId = alt.Id)
              AND (SELECT COUNT(*) FROM Quests q2
                   WHERE instr(lower(q2.Name), lower(q.Name)) > 0
                      OR instr(lower(ifnull(q2.NameKO, '')), lower(q.Name)) > 0
                      OR instr(lower(ifnull(q2.NameJA, '')), lower(q.Name)) > 0) = 1
            ORDER BY q.Name
            LIMIT 1";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(),
            "tarkov_data.db has no ungated quest with exactly one alternative matching the test constraints");
        return (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }

    /// <summary>The asset-db Id of the quest with this exact English name (QuestProgress rows are keyed by it).</summary>
    public static string QuestIdByName(string name)
    {
        using var connection = new SqliteConnection($"Data Source={AssetDbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Quests WHERE Name = $name";
        command.Parameters.AddWithValue("$name", name);
        var id = command.ExecuteScalar() as string;
        Assert.False(string.IsNullOrEmpty(id), $"tarkov_data.db has no quest named '{name}'");
        return id!;
    }

    /// <summary>Every quest's English display name, for spotting quest links in templated lists.</summary>
    public static HashSet<string> AllQuestNames()
    {
        using var connection = new SqliteConnection($"Data Source={AssetDbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Quests WHERE Name IS NOT NULL AND Name <> ''";
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var reader = command.ExecuteReader();
        while (reader.Read()) names.Add(reader.GetString(0));
        return names;
    }
}
