using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace TarkovHelper.Tests;

/// <summary>
/// End-to-end tests for map view-state persistence (see the
/// feature-persist-map-view-state PRD): launch the real app via the shared
/// <see cref="AppDriver"/> harness, drive the tab bar and read the map combo through
/// UI Automation, and assert the persisted user_data.db values.
///
/// Coverage gaps, on purpose: zoom/pan restore is asserted only via its persisted
/// round-trip (UIA cannot see render transforms — the on-screen values are guarded by
/// MapViewStatePersistenceTests plus manual checks), and raid precedence is unit-tested
/// only (driving a fake EFT log through the FileSystemWatcher is too fragile for CI).
/// </summary>
[Trait("Category", "E2E")]
public sealed class MapStateE2ETests : IDisposable
{
    private const string MapKeySetting = "map.lastSelectedMap";
    private const string ZoomSetting = "map.lastZoomLevel";
    private const string TranslateXSetting = "map.lastTranslateX";
    private const string TranslateYSetting = "map.lastTranslateY";

    private const string MapTab = "TabMap";
    private const string QuestsTab = "TabQuests";
    private const string MapCombo = "CmbMapSelect";
    private const string QuestsPageMarker = "LstQuests";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "TarkovHelperE2E", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [E2EFact]
    public void Saved_map_and_view_are_restored_on_launch_and_not_clobbered_on_close()
    {
        var configDir = NewConfigDir();
        E2EDb.CreateUserDataDb(configDir);
        E2EDb.SeedSetting(configDir, MapKeySetting, "Customs");
        E2EDb.SeedSetting(configDir, ZoomSetting, "1.5");
        E2EDb.SeedSetting(configDir, TranslateXSetting, "-250");
        E2EDb.SeedSetting(configDir, TranslateYSetting, "40");

        using var app = AppDriver.Launch(configDir);
        app.SelectTab(MapTab, MapCombo);

        // Pre-fix this showed Woods (index 0) and the close below then saved Woods back.
        Assert.Equal("Customs", app.WaitForComboSelection(MapCombo));

        app.CloseAndWaitForExit();

        Assert.Equal("Customs", E2EDb.ReadSetting(configDir, MapKeySetting));
        // The seeded view was applied (not reset to 100%/centered) and re-saved as-is:
        // a failed restore would leave zoom 1.0 and a centered translate here.
        Assert.Equal(1.5, ReadDouble(configDir, ZoomSetting));
        Assert.Equal(-250, ReadDouble(configDir, TranslateXSetting));
        Assert.Equal(40, ReadDouble(configDir, TranslateYSetting));
    }

    [E2EFact]
    public void Map_selection_survives_switching_tabs_and_back()
    {
        var configDir = NewConfigDir();
        E2EDb.CreateUserDataDb(configDir);
        E2EDb.SeedSetting(configDir, MapKeySetting, "Customs");

        using var app = AppDriver.Launch(configDir);
        app.SelectTab(MapTab, MapCombo);
        Assert.Equal("Customs", app.WaitForComboSelection(MapCombo));

        app.SelectTab(QuestsTab, QuestsPageMarker);
        app.SelectTab(MapTab, MapCombo);

        // The reported bug: returning to the Map tab reset the selection to Woods.
        Assert.Equal("Customs", app.WaitForComboSelection(MapCombo));

        app.CloseAndWaitForExit();

        Assert.Equal("Customs", E2EDb.ReadSetting(configDir, MapKeySetting));
    }

    [E2EFact]
    public void First_run_shows_the_first_configured_map_and_saves_it()
    {
        var configDir = NewConfigDir();

        using var app = AppDriver.Launch(configDir);
        app.SelectTab(MapTab, MapCombo);

        // No saved state: today's default behavior — first map in map_configs.json.
        Assert.Equal("Woods", app.WaitForComboSelection(MapCombo));

        app.CloseAndWaitForExit();

        Assert.Equal("Woods", E2EDb.ReadSetting(configDir, MapKeySetting));
    }

    #region Helpers

    private string NewConfigDir()
    {
        var dir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static double ReadDouble(string configDir, string key)
    {
        var value = E2EDb.ReadSetting(configDir, key);
        Assert.NotNull(value);
        return double.Parse(value!, CultureInfo.InvariantCulture);
    }

    #endregion
}
