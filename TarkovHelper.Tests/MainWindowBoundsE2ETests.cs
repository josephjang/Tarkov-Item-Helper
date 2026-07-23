using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TarkovHelper.Tests;

/// <summary>
/// End-to-end tests for main-window bounds persistence (see the
/// feature-persist-window-bounds PRD): they launch the real app via the shared
/// <see cref="AppDriver"/> harness, drive the actual window with Win32, and assert on
/// the on-screen geometry and the persisted user_data.db value.
/// </summary>
[Trait("Category", "E2E")]
public sealed class MainWindowBoundsE2ETests : IDisposable
{
    private const string BoundsKey = "app.mainWindowBounds";
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "TarkovHelperE2E", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [E2EFact]
    public void First_run_uses_defaults_and_saves_them_on_close()
    {
        var configDir = NewConfigDir();

        using var app = AppDriver.Launch(configDir);
        var rect = app.GetWindowRect();

        Assert.Equal(Win32.SW_SHOWNORMAL, app.GetShowCmd());
        AssertNear(1400, rect.Width);
        AssertNear(800, rect.Height);

        app.CloseAndWaitForExit();

        var saved = ReadSavedBounds(configDir);
        Assert.NotNull(saved);
        Assert.False(saved!.IsMaximized);
        AssertNear(rect.Left, saved.Left);
        AssertNear(rect.Top, saved.Top);
        AssertNear(rect.Width, saved.Width);
        AssertNear(rect.Height, saved.Height);
    }

    [E2EFact]
    public void Saved_bounds_are_restored_on_next_launch()
    {
        var configDir = NewConfigDir();
        E2EDb.CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, """{"Left":150,"Top":120,"Width":900,"Height":650,"IsMaximized":false}""");

        using var app = AppDriver.Launch(configDir);
        var rect = app.GetWindowRect();

        Assert.Equal(Win32.SW_SHOWNORMAL, app.GetShowCmd());
        AssertNear(150, rect.Left);
        AssertNear(120, rect.Top);
        AssertNear(900, rect.Width);
        AssertNear(650, rect.Height);

        app.CloseAndWaitForExit();

        // Nothing moved, so the close must save the same geometry back.
        var saved = ReadSavedBounds(configDir);
        Assert.NotNull(saved);
        AssertNear(150, saved!.Left);
        AssertNear(120, saved.Top);
        AssertNear(900, saved.Width);
        AssertNear(650, saved.Height);
    }

    [E2EFact]
    public void Maximized_close_reopens_maximized_and_unmaximizing_returns_normal_bounds()
    {
        var configDir = NewConfigDir();
        E2EDb.CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, """{"Left":150,"Top":120,"Width":900,"Height":650,"IsMaximized":false}""");

        using (var app = AppDriver.Launch(configDir))
        {
            app.ShowWindow(Win32.SW_MAXIMIZE);
            app.CloseAndWaitForExit();
        }

        var saved = ReadSavedBounds(configDir);
        Assert.NotNull(saved);
        Assert.True(saved!.IsMaximized);
        AssertNear(150, saved.Left); // RestoreBounds, not the maximized rect
        AssertNear(900, saved.Width);

        using (var app = AppDriver.Launch(configDir))
        {
            Assert.Equal(Win32.SW_SHOWMAXIMIZED, app.GetShowCmd());

            app.ShowWindow(Win32.SW_RESTORE);
            var rect = app.GetWindowRect();
            AssertNear(150, rect.Left);
            AssertNear(120, rect.Top);
            AssertNear(900, rect.Width);
            AssertNear(650, rect.Height);

            app.CloseAndWaitForExit();
        }
    }

    [E2EFact]
    public void Minimized_close_reopens_as_a_normal_window_at_the_last_bounds()
    {
        var configDir = NewConfigDir();
        E2EDb.CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, """{"Left":150,"Top":120,"Width":900,"Height":650,"IsMaximized":false}""");

        using (var app = AppDriver.Launch(configDir))
        {
            app.ShowWindow(Win32.SW_MINIMIZE);
            app.CloseAndWaitForExit();
        }

        var saved = ReadSavedBounds(configDir);
        Assert.NotNull(saved);
        Assert.False(saved!.IsMaximized);
        AssertNear(150, saved.Left); // RestoreBounds, not the minimized (-32000,-32000) parking rect
        AssertNear(650, saved.Height);

        using (var app = AppDriver.Launch(configDir))
        {
            Assert.Equal(Win32.SW_SHOWNORMAL, app.GetShowCmd());
            app.CloseAndWaitForExit();
        }
    }

    [E2EFact]
    public void Off_screen_bounds_fall_back_to_the_centered_defaults()
    {
        var configDir = NewConfigDir();
        E2EDb.CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, """{"Left":-9000,"Top":-9000,"Width":1000,"Height":700,"IsMaximized":false}""");

        using var app = AppDriver.Launch(configDir);
        var rect = app.GetWindowRect();

        AssertNear(1400, rect.Width);
        AssertNear(800, rect.Height);
        Assert.True(app.IsWithinVirtualScreen(), $"window at ({rect.Left},{rect.Top}) is off-screen");

        app.CloseAndWaitForExit();
    }

    [E2EFact]
    public void Corrupt_saved_value_starts_at_defaults_and_self_heals_on_close()
    {
        var configDir = NewConfigDir();
        E2EDb.CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, "not json at all");

        using var app = AppDriver.Launch(configDir);
        var rect = app.GetWindowRect();

        AssertNear(1400, rect.Width);
        AssertNear(800, rect.Height);

        app.CloseAndWaitForExit();

        var saved = ReadSavedBounds(configDir);
        Assert.NotNull(saved);
        Assert.True(saved!.Width > 0, "corrupt value was not replaced with valid bounds");
    }

    #region Helpers

    private string NewConfigDir()
    {
        var dir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void AssertNear(double expected, double actual, double tolerance = 2.0)
        => Assert.InRange(actual, expected - tolerance, expected + tolerance);

    private sealed class SavedBounds
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
    }

    /// <summary>Reads the persisted bounds JSON, or null when the key/db is missing.</summary>
    private static SavedBounds? ReadSavedBounds(string configDir)
    {
        var value = E2EDb.ReadSetting(configDir, BoundsKey);
        return value == null ? null : JsonSerializer.Deserialize<SavedBounds>(value);
    }

    private static void SeedSavedBounds(string configDir, string value)
        => E2EDb.SeedSetting(configDir, BoundsKey, value);

    #endregion
}
