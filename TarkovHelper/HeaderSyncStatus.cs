using TarkovHelper.Models;

namespace TarkovHelper;

/// <summary>
/// State shown by the title-bar sync/raid status chip.
/// </summary>
public enum SyncChipState
{
    /// <summary>No log monitoring active (gray dot).</summary>
    Off,

    /// <summary>Monitoring logs, no raid activity (green dot).</summary>
    Watching,

    /// <summary>Matching or connecting to a raid (amber dot).</summary>
    Matching,

    /// <summary>In a live raid (gold dot).</summary>
    InRaid
}

/// <summary>
/// Pure (monitoring, raid-state) → chip-state mapping for the title-bar sync/raid
/// status chip. Kept free of WPF types so it is unit-testable (same pattern as
/// <see cref="HeaderLayout"/>); MainWindow renders the resulting state to text and
/// dot color.
/// </summary>
public static class HeaderSyncStatus
{
    public static SyncChipState GetState(bool monitoring, RaidState? raidState)
    {
        // Raid state is only meaningful while monitoring — a stale CurrentRaid from a
        // previous session must not light the chip when the watcher is off.
        if (!monitoring) return SyncChipState.Off;

        return raidState switch
        {
            RaidState.InRaid => SyncChipState.InRaid,
            RaidState.Matching or RaidState.Connecting => SyncChipState.Matching,
            _ => SyncChipState.Watching, // Idle, Ended, or no raid info yet
        };
    }
}
