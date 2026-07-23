using TarkovHelper.Models;

namespace TarkovHelper.Services.Map;

/// <summary>
/// Pure core of the map view-state persistence (last viewed map/zoom/pan across tab
/// switches and launches, see the feature-persist-map-view-state PRD). Kept free of
/// UI/DB/service dependencies so it is unit-testable: MapPage supplies the live values
/// (saved settings, available map keys, current raid) and owns storage and UI wiring.
/// </summary>
public static class MapViewStatePersistence
{
    /// <summary>Where the initial-map decision came from.</summary>
    public enum MapChoiceSource
    {
        /// <summary>A raid is live — its map wins over any saved state.</summary>
        ActiveRaid,

        /// <summary>The saved last-viewed map (restore zoom/pan along with it).</summary>
        Saved,

        /// <summary>First run or unusable saved key — first configured map, default view.</summary>
        Default
    }

    /// <summary>The map to show on first load, with the reason it was chosen.</summary>
    public sealed record MapChoice(string MapKey, MapChoiceSource Source);

    /// <summary>A validated saved view (zoom/pan) to reapply instead of the centered default.</summary>
    public sealed record MapView(double ZoomLevel, double TranslateX, double TranslateY);

    /// <summary>
    /// Decides which map to show on first load. Precedence: live raid > saved map >
    /// first configured map. Keys are matched case-insensitively and the returned key
    /// is the canonical entry from <paramref name="availableMapKeys"/> (so combo-item
    /// Tag lookups match exactly). An unknown raid/saved key falls through to the next
    /// precedence level. Returns null only when no maps are configured.
    /// </summary>
    public static MapChoice? DecideInitialMap(
        string? savedMapKey,
        IReadOnlyList<string> availableMapKeys,
        string? activeRaidMapKey)
    {
        if (availableMapKeys == null || availableMapKeys.Count == 0) return null;

        var raidKey = FindCanonicalKey(activeRaidMapKey, availableMapKeys);
        if (raidKey != null) return new MapChoice(raidKey, MapChoiceSource.ActiveRaid);

        var savedKey = FindCanonicalKey(savedMapKey, availableMapKeys);
        if (savedKey != null) return new MapChoice(savedKey, MapChoiceSource.Saved);

        return new MapChoice(availableMapKeys[0], MapChoiceSource.Default);
    }

    /// <summary>
    /// Validates a saved zoom/pan view: all values must be finite, and the zoom is
    /// clamped into [<paramref name="minZoom"/>, <paramref name="maxZoom"/>]. Returns
    /// null when unusable — callers show the default centered 100% view instead.
    /// </summary>
    public static MapView? ValidateView(
        double zoomLevel, double translateX, double translateY,
        double minZoom, double maxZoom)
    {
        if (!IsFinite(zoomLevel) || !IsFinite(translateX) || !IsFinite(translateY))
            return null;

        return new MapView(Math.Clamp(zoomLevel, minZoom, maxZoom), translateX, translateY);
    }

    /// <summary>
    /// MapKey of a raid that is currently live, else null. Live = a raid in the
    /// Matching/Connecting/InRaid state with a known map (RaidStarted fires at
    /// Matching; every end path sets Ended or clears the raid, and Idle raids have
    /// no map to honor).
    /// </summary>
    public static string? GetActiveRaidMapKey(EftRaidInfo? raid)
    {
        if (raid == null) return null;
        if (raid.State is not (RaidState.Matching or RaidState.Connecting or RaidState.InRaid))
            return null;
        return string.IsNullOrEmpty(raid.MapKey) ? null : raid.MapKey;
    }

    /// <summary>Case-insensitive lookup returning the canonical key from the config list.</summary>
    private static string? FindCanonicalKey(string? key, IReadOnlyList<string> availableMapKeys)
    {
        if (string.IsNullOrEmpty(key)) return null;

        foreach (var candidate in availableMapKeys)
        {
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
