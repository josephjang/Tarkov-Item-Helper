using System.Text.Json;
using System.Windows;
using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Guards the main-window bounds persistence (size/position/state across launches, see the
/// feature-persist-window-bounds PRD). Exercises the pure static
/// <see cref="WindowBoundsPersistence"/> core with synthetic screens/rects, so no Window or
/// DB is needed. The end-to-end behavior (constructor-time restore, save on Closing) was
/// verified against the running app when the feature landed.
/// </summary>
public class WindowBoundsPersistenceTests
{
    // A 2560x1080 primary monitor; MinWidth/MinHeight match MainWindow.xaml (600/400).
    private static readonly Rect Screen = new(0, 0, 2560, 1080);
    private const double MinW = 600;
    private const double MinH = 400;

    private static WindowBoundsPersistence.WindowBounds? Parse(string? json, Rect? screen = null)
        => WindowBoundsPersistence.ParseAndValidate(json, MinW, MinH, screen ?? Screen);

    [Fact]
    public void Normal_close_round_trips_the_live_bounds()
    {
        var json = WindowBoundsPersistence.CreateSaveValue(
            WindowState.Normal, new Rect(200, 50, 800, 600), restoreBounds: Rect.Empty);

        Assert.NotNull(json);
        var restored = Parse(json);

        Assert.NotNull(restored);
        Assert.Equal(200, restored!.Left);
        Assert.Equal(50, restored.Top);
        Assert.Equal(800, restored.Width);
        Assert.Equal(600, restored.Height);
        Assert.False(restored.IsMaximized);
    }

    [Fact]
    public void Maximized_close_saves_restore_bounds_with_the_maximized_flag()
    {
        // Live bounds are the maximized rect and must NOT be saved.
        var json = WindowBoundsPersistence.CreateSaveValue(
            WindowState.Maximized, new Rect(-7, -7, 2573, 1045), new Rect(200, 50, 800, 600));

        var restored = Parse(json);

        Assert.NotNull(restored);
        Assert.True(restored!.IsMaximized);
        Assert.Equal(200, restored.Left);
        Assert.Equal(50, restored.Top);
        Assert.Equal(800, restored.Width);
        Assert.Equal(600, restored.Height);
    }

    [Fact]
    public void Minimized_close_saves_normal_bounds_without_the_maximized_flag()
    {
        // Windows parks minimized windows at (-32000,-32000); RestoreBounds must win
        // and the window must reopen as Normal.
        var json = WindowBoundsPersistence.CreateSaveValue(
            WindowState.Minimized, new Rect(-32000, -32000, 160, 28), new Rect(200, 50, 800, 600));

        var restored = Parse(json);

        Assert.NotNull(restored);
        Assert.False(restored!.IsMaximized);
        Assert.Equal(200, restored.Left);
        Assert.Equal(50, restored.Top);
    }

    [Fact]
    public void Unusable_geometry_is_not_saved_so_the_previous_value_survives()
    {
        // Empty RestoreBounds (window never left Normal state before maximizing at startup).
        Assert.Null(WindowBoundsPersistence.CreateSaveValue(
            WindowState.Maximized, new Rect(0, 0, 2560, 1080), Rect.Empty));

        // Zero-sized live bounds.
        Assert.Null(WindowBoundsPersistence.CreateSaveValue(
            WindowState.Normal, new Rect(100, 100, 0, 0), Rect.Empty));
    }

    [Fact]
    public void First_run_has_nothing_to_restore()
    {
        Assert.Null(Parse(null));
        Assert.Null(Parse(""));
        Assert.Null(Parse("null"));
    }

    [Fact]
    public void Malformed_json_throws_so_the_caller_can_log_it()
    {
        Assert.ThrowsAny<JsonException>(() => Parse("not json at all"));
    }

    [Fact]
    public void Off_screen_position_falls_back_to_defaults()
    {
        // Monitor unplugged: saved position far outside the remaining virtual screen.
        Assert.Null(Parse("""{"Left":-9000,"Top":-9000,"Width":1000,"Height":700,"IsMaximized":false}"""));

        // Entirely below the screen (title bar not reachable).
        Assert.Null(Parse("""{"Left":100,"Top":2000,"Width":1000,"Height":700,"IsMaximized":false}"""));

        // Non-finite coordinates (1e999 parses to Infinity).
        Assert.Null(Parse("""{"Left":1e999,"Top":50,"Width":1000,"Height":700,"IsMaximized":false}"""));
    }

    [Fact]
    public void Position_on_a_secondary_monitor_is_kept_while_that_monitor_exists()
    {
        // Virtual screen spanning a 1920x1080 monitor left of the 2560x1080 primary:
        // negative coordinates are valid there and must survive.
        var span = new Rect(-1920, 0, 4480, 1080);
        var restored = Parse(
            """{"Left":-1800,"Top":100,"Width":1000,"Height":700,"IsMaximized":false}""", span);

        Assert.NotNull(restored);
        Assert.Equal(-1800, restored!.Left);

        // Same value against the primary-only screen (monitor unplugged) is rejected.
        Assert.Null(Parse("""{"Left":-1800,"Top":100,"Width":1000,"Height":700,"IsMaximized":false}"""));
    }

    [Fact]
    public void Size_is_clamped_to_the_screen_and_the_window_minimums()
    {
        var restored = Parse("""{"Left":10,"Top":10,"Width":99999,"Height":99999,"IsMaximized":false}""");
        Assert.NotNull(restored);
        Assert.Equal(Screen.Width, restored!.Width);
        Assert.Equal(Screen.Height, restored.Height);

        restored = Parse("""{"Left":10,"Top":10,"Width":10,"Height":10,"IsMaximized":false}""");
        Assert.NotNull(restored);
        Assert.Equal(MinW, restored!.Width);
        Assert.Equal(MinH, restored.Height);
    }

    [Fact]
    public void Partially_off_screen_is_kept_only_while_the_title_bar_stays_usable()
    {
        // Hanging off the right edge with 150px of title strip still visible: kept.
        Assert.NotNull(Parse(
            $$"""{"Left":{{Screen.Width - 150}},"Top":10,"Width":1000,"Height":700,"IsMaximized":false}"""));

        // Only 50px visible: rejected.
        Assert.Null(Parse(
            $$"""{"Left":{{Screen.Width - 50}},"Top":10,"Width":1000,"Height":700,"IsMaximized":false}"""));
    }

    [Fact]
    public void Json_contract_matches_the_stored_data_format()
    {
        // Property names live in user_data.db (key app.mainWindowBounds); renaming them
        // would silently discard every user's saved geometry.
        var json = WindowBoundsPersistence.CreateSaveValue(
            WindowState.Normal, new Rect(1, 2, 700, 500), Rect.Empty)!;

        using var doc = JsonDocument.Parse(json);
        foreach (var name in new[] { "Left", "Top", "Width", "Height", "IsMaximized" })
        {
            Assert.True(doc.RootElement.TryGetProperty(name, out _), $"missing property '{name}'");
        }
    }
}
