using TarkovHelper.Models;
using TarkovHelper.Services.Map;
using static TarkovHelper.Services.Map.MapViewStatePersistence;

namespace TarkovHelper.Tests;

/// <summary>
/// Guards the pure decision core of the map view-state persistence
/// (see the feature-persist-map-view-state PRD): which map to show on first load
/// (raid > saved > default), raid-liveness detection, and saved zoom/pan validation.
/// </summary>
public sealed class MapViewStatePersistenceTests
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 5.0;

    private static readonly string[] Maps = { "Woods", "Customs", "Factory" };

    private static EftRaidInfo Raid(RaidState state, string? mapKey = "Customs") =>
        new() { State = state, MapKey = mapKey, RaidType = RaidType.PMC };

    #region DecideInitialMap

    [Fact]
    public void Saved_map_is_chosen_when_no_raid_is_live()
    {
        var choice = DecideInitialMap("Customs", Maps, activeRaidMapKey: null);

        Assert.NotNull(choice);
        Assert.Equal("Customs", choice!.MapKey);
        Assert.Equal(MapChoiceSource.Saved, choice.Source);
    }

    [Fact]
    public void Saved_map_matches_case_insensitively_and_returns_the_canonical_key()
    {
        var choice = DecideInitialMap("cUsToMs", Maps, activeRaidMapKey: null);

        // The canonical config key, not the saved spelling — combo Tag lookups are exact.
        Assert.Equal("Customs", choice!.MapKey);
        Assert.Equal(MapChoiceSource.Saved, choice.Source);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MapRemovedFromConfigs")]
    public void Missing_or_unknown_saved_map_falls_back_to_the_first_map(string? savedKey)
    {
        var choice = DecideInitialMap(savedKey, Maps, activeRaidMapKey: null);

        Assert.Equal("Woods", choice!.MapKey);
        Assert.Equal(MapChoiceSource.Default, choice.Source);
    }

    [Fact]
    public void Live_raid_map_beats_the_saved_map()
    {
        var choice = DecideInitialMap("Customs", Maps, activeRaidMapKey: "Factory");

        Assert.Equal("Factory", choice!.MapKey);
        Assert.Equal(MapChoiceSource.ActiveRaid, choice.Source);
    }

    [Fact]
    public void Raid_map_key_is_canonicalized_too()
    {
        var choice = DecideInitialMap(null, Maps, activeRaidMapKey: "factory");

        Assert.Equal("Factory", choice!.MapKey);
        Assert.Equal(MapChoiceSource.ActiveRaid, choice.Source);
    }

    [Fact]
    public void Unknown_raid_map_is_ignored_and_the_saved_map_wins()
    {
        var choice = DecideInitialMap("Customs", Maps, activeRaidMapKey: "NotAConfiguredMap");

        Assert.Equal("Customs", choice!.MapKey);
        Assert.Equal(MapChoiceSource.Saved, choice.Source);
    }

    [Fact]
    public void Empty_map_list_returns_null()
    {
        Assert.Null(DecideInitialMap("Customs", Array.Empty<string>(), "Customs"));
    }

    #endregion

    #region GetActiveRaidMapKey

    [Fact]
    public void Null_raid_is_not_live()
    {
        Assert.Null(GetActiveRaidMapKey(null));
    }

    [Theory]
    [InlineData(RaidState.Idle)]
    [InlineData(RaidState.Ended)]
    public void Idle_and_ended_raids_are_not_live(RaidState state)
    {
        Assert.Null(GetActiveRaidMapKey(Raid(state)));
    }

    [Theory]
    [InlineData(RaidState.Matching)]
    [InlineData(RaidState.Connecting)]
    [InlineData(RaidState.InRaid)]
    public void Matching_connecting_and_inraid_raids_are_live(RaidState state)
    {
        Assert.Equal("Customs", GetActiveRaidMapKey(Raid(state)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Live_raid_without_a_map_key_yields_null(string? mapKey)
    {
        Assert.Null(GetActiveRaidMapKey(Raid(RaidState.InRaid, mapKey)));
    }

    #endregion

    #region ValidateView

    [Fact]
    public void Valid_view_round_trips()
    {
        var view = ValidateView(1.5, -320.25, 48.0, MinZoom, MaxZoom);

        Assert.NotNull(view);
        Assert.Equal(1.5, view!.ZoomLevel);
        Assert.Equal(-320.25, view.TranslateX);
        Assert.Equal(48.0, view.TranslateY);
    }

    [Fact]
    public void Zero_translate_is_a_legitimate_pan()
    {
        var view = ValidateView(1.0, 0.0, 0.0, MinZoom, MaxZoom);

        Assert.NotNull(view);
        Assert.Equal(0.0, view!.TranslateX);
        Assert.Equal(0.0, view.TranslateY);
    }

    [Theory]
    [InlineData(0.01, MinZoom)]  // below range (e.g. hand-edited db value)
    [InlineData(0.0, MinZoom)]
    [InlineData(99.0, MaxZoom)]  // above range
    public void Out_of_range_zoom_is_clamped(double savedZoom, double expected)
    {
        var view = ValidateView(savedZoom, 10, 10, MinZoom, MaxZoom);

        Assert.Equal(expected, view!.ZoomLevel);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(1.0, double.NaN, 0)]
    [InlineData(1.0, 0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0, 0)]
    [InlineData(1.0, double.NegativeInfinity, 0)]
    [InlineData(1.0, 0, double.PositiveInfinity)]
    public void Non_finite_values_reject_the_whole_view(double zoom, double tx, double ty)
    {
        Assert.Null(ValidateView(zoom, tx, ty, MinZoom, MaxZoom));
    }

    #endregion
}
