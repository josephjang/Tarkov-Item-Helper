namespace TarkovHelper.Tests;

/// <summary>
/// Unit-level guards that the E2EQuestData asset-db queries still find a row: a
/// tarkov_data.db update that silently empties any of them would otherwise only
/// surface as an e2e failure on an interactive desktop. These run in the plain
/// unit suite because the asset db is copied to this test output too.
/// </summary>
public sealed class E2EQuestDataTests
{
    [Fact]
    public void Locked_quest_with_active_prereq_exists()
    {
        var (questName, prereqName) = E2EQuestData.FindLockedQuestWithActivePrereq();
        Assert.False(string.IsNullOrWhiteSpace(questName));
        Assert.False(string.IsNullOrWhiteSpace(prereqName));
        Assert.NotEqual(questName, prereqName);
    }

    [Fact]
    public void Standalone_active_quest_exists()
    {
        Assert.False(string.IsNullOrWhiteSpace(E2EQuestData.FindStandaloneActiveQuest()));
    }

    [Fact]
    public void Quest_with_single_alternative_exists_and_resolves_ids()
    {
        var (questName, altName, questId, altId) = E2EQuestData.FindQuestWithSingleAlternative();
        Assert.False(string.IsNullOrWhiteSpace(altName));
        Assert.Equal(questId, E2EQuestData.QuestIdByName(questName));
        Assert.Equal(altId, E2EQuestData.QuestIdByName(altName));
    }
}
