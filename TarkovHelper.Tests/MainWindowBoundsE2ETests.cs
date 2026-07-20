using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TarkovHelper.Tests;

/// <summary>
/// End-to-end tests for main-window bounds persistence (see the
/// feature-persist-window-bounds PRD): they launch the real app process
/// (<c>dotnet TarkovHelper.dll</c> - running the DLL bypasses the requireAdministrator
/// apphost manifest), point it at a throwaway Config folder via the
/// TARKOVHELPER_CONFIG_PATH environment variable, drive the actual window with Win32,
/// and assert on the on-screen geometry and the persisted user_data.db value.
///
/// These need an interactive desktop and take a few seconds per launch; exclude them
/// from quick runs with <c>dotnet test --filter Category!=E2E</c>. They skip
/// automatically when the app build output is missing.
///
/// Coordinates: the test host is DPI-unaware, so GetWindowRect returns virtualized
/// 96-DPI coordinates that match WPF's device-independent units 1:1 even on scaled
/// displays (verified on a 200% display) - values below are plain WPF units.
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

        using var app = App.Launch(configDir);
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
        CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, """{"Left":150,"Top":120,"Width":900,"Height":650,"IsMaximized":false}""");

        using var app = App.Launch(configDir);
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
        CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, """{"Left":150,"Top":120,"Width":900,"Height":650,"IsMaximized":false}""");

        using (var app = App.Launch(configDir))
        {
            app.ShowWindow(Win32.SW_MAXIMIZE);
            app.CloseAndWaitForExit();
        }

        var saved = ReadSavedBounds(configDir);
        Assert.NotNull(saved);
        Assert.True(saved!.IsMaximized);
        AssertNear(150, saved.Left); // RestoreBounds, not the maximized rect
        AssertNear(900, saved.Width);

        using (var app = App.Launch(configDir))
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
        CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, """{"Left":150,"Top":120,"Width":900,"Height":650,"IsMaximized":false}""");

        using (var app = App.Launch(configDir))
        {
            app.ShowWindow(Win32.SW_MINIMIZE);
            app.CloseAndWaitForExit();
        }

        var saved = ReadSavedBounds(configDir);
        Assert.NotNull(saved);
        Assert.False(saved!.IsMaximized);
        AssertNear(150, saved.Left); // RestoreBounds, not the minimized (-32000,-32000) parking rect
        AssertNear(650, saved.Height);

        using (var app = App.Launch(configDir))
        {
            Assert.Equal(Win32.SW_SHOWNORMAL, app.GetShowCmd());
            app.CloseAndWaitForExit();
        }
    }

    [E2EFact]
    public void Off_screen_bounds_fall_back_to_the_centered_defaults()
    {
        var configDir = NewConfigDir();
        CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, """{"Left":-9000,"Top":-9000,"Width":1000,"Height":700,"IsMaximized":false}""");

        using var app = App.Launch(configDir);
        var rect = app.GetWindowRect();

        AssertNear(1400, rect.Width);
        AssertNear(800, rect.Height);
        Assert.True(Win32.IsWithinVirtualScreen(rect), $"window at ({rect.Left},{rect.Top}) is off-screen");

        app.CloseAndWaitForExit();
    }

    [E2EFact]
    public void Corrupt_saved_value_starts_at_defaults_and_self_heals_on_close()
    {
        var configDir = NewConfigDir();
        CreateUserDataDb(configDir);
        SeedSavedBounds(configDir, "not json at all");

        using var app = App.Launch(configDir);
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
        var dbPath = Path.Combine(configDir, "user_data.db");
        if (!File.Exists(dbPath)) return null;

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM UserSettings WHERE Key = $key";
        command.Parameters.AddWithValue("$key", BoundsKey);
        var value = command.ExecuteScalar() as string;
        return value == null ? null : JsonSerializer.Deserialize<SavedBounds>(value);
    }

    /// <summary>
    /// Creates a minimal user_data.db so tests can seed bounds without a first app
    /// launch. The app's own schema creation is CREATE TABLE IF NOT EXISTS, so it
    /// adopts this file and adds the remaining tables on startup.
    /// </summary>
    private static void CreateUserDataDb(string configDir)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(configDir, "user_data.db")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS UserSettings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)";
        command.ExecuteNonQuery();
    }

    private static void SeedSavedBounds(string configDir, string value)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(configDir, "user_data.db")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO UserSettings (Key, Value) VALUES ($key, $value) " +
            "ON CONFLICT(Key) DO UPDATE SET Value = $value";
        command.Parameters.AddWithValue("$key", BoundsKey);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    #endregion

    #region App driver

    /// <summary>One launched instance of the real app, tracked by its main window handle.</summary>
    private sealed class App : IDisposable
    {
        private readonly Process _process;
        private readonly IntPtr _hwnd;

        private App(Process process, IntPtr hwnd)
        {
            _process = process;
            _hwnd = hwnd;
        }

        public static App Launch(string configDir)
        {
            var dll = AppUnderTest.DllPath!;
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(dll)!,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(dll);
            psi.Environment["TARKOVHELPER_CONFIG_PATH"] = configDir;

            var process = Process.Start(psi)!;
            try
            {
                return new App(process, WaitForMainWindow(process));
            }
            catch
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Waits for the process's "Tarkov Helper" top-level window. Matched by exact
        /// title because debug builds also open a "Debug Toolbox" window, which makes
        /// Process.MainWindowHandle ambiguous.
        /// </summary>
        private static IntPtr WaitForMainWindow(Process process)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                    throw new InvalidOperationException($"app exited during startup (exit code {process.ExitCode})");

                var hwnd = Win32.FindTopLevelWindow(process.Id, "Tarkov Helper");
                if (hwnd != IntPtr.Zero) return hwnd;

                Thread.Sleep(250);
            }
            throw new TimeoutException("main window did not appear within 60s");
        }

        public Win32.WindowRect GetWindowRect()
        {
            Win32.GetWindowRect(_hwnd, out var rect);
            return new Win32.WindowRect(rect);
        }

        /// <summary>SW_SHOWNORMAL / SW_SHOWMINIMIZED / SW_SHOWMAXIMIZED of the live window.</summary>
        public int GetShowCmd()
        {
            var placement = Win32.WINDOWPLACEMENT.Create();
            Win32.GetWindowPlacement(_hwnd, ref placement);
            return placement.showCmd;
        }

        public void ShowWindow(int cmd)
        {
            Win32.ShowWindow(_hwnd, cmd);
            Thread.Sleep(300); // let WPF finish the state change before we act on it
        }

        /// <summary>Graceful close (WM_CLOSE, so the Closing event saves) and wait for exit.</summary>
        public void CloseAndWaitForExit()
        {
            Win32.PostMessage(_hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            Assert.True(_process.WaitForExit(20_000), "app did not exit within 20s of WM_CLOSE");
        }

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
                _process.Dispose();
            }
            catch { /* best effort */ }
        }
    }

    #endregion
}

/// <summary>Skips e2e tests when the app build output is not present.</summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (AppUnderTest.DllPath == null)
            Skip = "TarkovHelper build output not found - build TarkovHelper.csproj first";
    }
}

/// <summary>Locates the app DLL matching this test build's configuration.</summary>
internal static class AppUnderTest
{
    public static readonly string? DllPath = Locate();

    private static string? Locate()
    {
        // ...\TarkovHelper.Tests\bin\<Configuration>\net8.0-windows\ up to the repo root
        var tfmDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = tfmDir.Parent?.Name;
        var repoRoot = tfmDir.Parent?.Parent?.Parent?.Parent;
        if (configuration == null || repoRoot == null) return null;

        var dll = Path.Combine(repoRoot.FullName, "TarkovHelper", "bin", configuration, tfmDir.Name, "TarkovHelper.dll");
        return File.Exists(dll) ? dll : null;
    }
}

/// <summary>Minimal user32 interop for driving the app window from the tests.</summary>
internal static class Win32
{
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_MAXIMIZE = 3;
    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;
    public const uint WM_CLOSE = 0x0010;

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length, flags, showCmd;
        public POINT minPosition, maxPosition;
        public RECT normalPosition;

        public static WINDOWPLACEMENT Create()
            => new() { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
    }

    public readonly struct WindowRect
    {
        public WindowRect(RECT r) { Left = r.Left; Top = r.Top; Width = r.Right - r.Left; Height = r.Bottom - r.Top; }
        public double Left { get; }
        public double Top { get; }
        public double Width { get; }
        public double Height { get; }
    }

    public static bool IsWithinVirtualScreen(WindowRect rect)
    {
        double left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        double top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        return rect.Left >= left && rect.Top >= top &&
               rect.Left + rect.Width <= left + GetSystemMetrics(SM_CXVIRTUALSCREEN) &&
               rect.Top + rect.Height <= top + GetSystemMetrics(SM_CYVIRTUALSCREEN);
    }

    /// <summary>Finds a visible top-level window of the process with the exact title.</summary>
    public static IntPtr FindTopLevelWindow(int processId, string title)
    {
        var found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid != processId || !IsWindowVisible(hwnd)) return true;

            var text = new StringBuilder(256);
            GetWindowText(hwnd, text, text.Capacity);
            if (text.ToString() != title) return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}
