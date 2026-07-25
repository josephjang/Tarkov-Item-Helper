namespace TarkovHelper;

/// <summary>
/// Density of the main-window header at the current window width.
/// </summary>
public enum HeaderLayoutMode
{
    /// <summary>Everything visible.</summary>
    Full,

    /// <summary>Sync-status chip shows only its colored dot; tab glyphs are hidden.</summary>
    Compact,

    /// <summary>Compact, plus the brand title is hidden.</summary>
    Minimal
}

/// <summary>
/// Pure width→mode mapping for the main-window header so it degrades gracefully
/// instead of clipping at narrow widths (window MinWidth is 600). Kept free of
/// WPF types so it is unit-testable; MainWindow applies the resulting mode.
/// </summary>
public static class HeaderLayout
{
    /// <summary>
    /// Below this width the sync-status text is hidden (dot + tooltip remain) and the
    /// tab glyphs are dropped — text-only tabs fit down to the window minimum.
    /// </summary>
    public const double CompactThreshold = 1000;

    /// <summary>Below this width the brand title is hidden as well.</summary>
    public const double MinimalThreshold = 760;

    public static HeaderLayoutMode GetMode(double windowWidth)
    {
        if (windowWidth < MinimalThreshold) return HeaderLayoutMode.Minimal;
        if (windowWidth < CompactThreshold) return HeaderLayoutMode.Compact;
        return HeaderLayoutMode.Full;
    }
}
