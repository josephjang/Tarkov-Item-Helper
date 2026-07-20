using System.Text.Json;
using System.Windows;

namespace TarkovHelper.Services;

/// <summary>
/// Pure core of the main-window bounds persistence (size/position/state across launches,
/// see the feature-persist-window-bounds PRD). Kept free of Window/service dependencies so
/// it is unit-testable: MainWindow supplies the live values (SystemParameters, window state)
/// and owns storage and logging.
/// </summary>
public static class WindowBoundsPersistence
{
    /// <summary>
    /// JSON contract of the saved value. Property names are part of the stored data
    /// (user_data.db, key app.mainWindowBounds) — do not rename.
    /// </summary>
    public sealed class WindowBounds
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
    }

    /// <summary>
    /// Parses a saved JSON value and validates/clamps it against the given virtual screen.
    /// Returns null when there is nothing valid to restore (first run, off-screen position,
    /// non-finite values) — callers keep the XAML defaults then. Malformed JSON throws
    /// <see cref="JsonException"/> so callers can log it.
    /// </summary>
    public static WindowBounds? ParseAndValidate(string? json, double minWidth, double minHeight, Rect virtualScreen)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var bounds = JsonSerializer.Deserialize<WindowBounds>(json);
        if (bounds == null) return null;

        if (!IsFinite(bounds.Left) || !IsFinite(bounds.Top) ||
            !IsFinite(bounds.Width) || !IsFinite(bounds.Height))
        {
            return null;
        }

        bounds.Width = Math.Clamp(bounds.Width, minWidth, Math.Max(minWidth, virtualScreen.Width));
        bounds.Height = Math.Clamp(bounds.Height, minHeight, Math.Max(minHeight, virtualScreen.Height));

        return IsPositionVisible(bounds, virtualScreen) ? bounds : null;
    }

    /// <summary>
    /// Guards against saved bounds on an unplugged/rearranged monitor: a usable slice of
    /// the title-bar strip must intersect the virtual screen.
    /// </summary>
    public static bool IsPositionVisible(WindowBounds bounds, Rect virtualScreen)
    {
        var titleStrip = new Rect(bounds.Left, bounds.Top, bounds.Width, 40);
        titleStrip.Intersect(virtualScreen);
        return titleStrip != Rect.Empty && titleStrip.Width >= 100 && titleStrip.Height >= 20;
    }

    /// <summary>
    /// Builds the JSON value to save at close, or null when the geometry is unusable
    /// (callers keep the previously saved value then). Normal state saves the live bounds;
    /// Maximized/Minimized (incl. F11 fullscreen, which reads as Maximized) save
    /// restoreBounds — the last normal bounds — so un-maximizing next session restores
    /// sane geometry. Only Maximized is recorded as maximized: a minimized close reopens
    /// as a normal window.
    /// </summary>
    public static string? CreateSaveValue(WindowState state, Rect liveBounds, Rect restoreBounds)
    {
        var rect = state == WindowState.Normal ? liveBounds : restoreBounds;

        if (rect == Rect.Empty || rect.Width <= 0 || rect.Height <= 0 ||
            !IsFinite(rect.X) || !IsFinite(rect.Y) || !IsFinite(rect.Width) || !IsFinite(rect.Height))
        {
            return null;
        }

        return JsonSerializer.Serialize(new WindowBounds
        {
            Left = rect.Left,
            Top = rect.Top,
            Width = rect.Width,
            Height = rect.Height,
            IsMaximized = state == WindowState.Maximized
        });
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
