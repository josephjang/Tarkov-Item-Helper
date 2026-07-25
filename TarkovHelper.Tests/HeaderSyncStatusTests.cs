using TarkovHelper.Models;

namespace TarkovHelper.Tests;

/// <summary>
/// State mapping for the title-bar sync/raid status chip (top-bar redesign):
/// monitoring-off always wins (a stale CurrentRaid must not light the chip), and the
/// raid state distinguishes matching/connecting from a live raid.
/// </summary>
public class HeaderSyncStatusTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(RaidState.Idle)]
    [InlineData(RaidState.Matching)]
    [InlineData(RaidState.Connecting)]
    [InlineData(RaidState.InRaid)]
    [InlineData(RaidState.Ended)]
    public void Not_monitoring_is_always_off(RaidState? raidState)
        => Assert.Equal(SyncChipState.Off, HeaderSyncStatus.GetState(monitoring: false, raidState));

    [Theory]
    [InlineData(null, SyncChipState.Watching)]                  // no raid info yet
    [InlineData(RaidState.Idle, SyncChipState.Watching)]
    [InlineData(RaidState.Ended, SyncChipState.Watching)]       // back out of a raid
    [InlineData(RaidState.Matching, SyncChipState.Matching)]
    [InlineData(RaidState.Connecting, SyncChipState.Matching)]
    [InlineData(RaidState.InRaid, SyncChipState.InRaid)]
    public void Monitoring_maps_raid_state_to_chip_state(RaidState? raidState, SyncChipState expected)
        => Assert.Equal(expected, HeaderSyncStatus.GetState(monitoring: true, raidState));
}
