using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Microsoft.Data.Sqlite;

namespace TarkovHelper.Tests;

/// <summary>
/// Shared harness for end-to-end tests that drive the real app (extracted from
/// MainWindowBoundsE2ETests for reuse; see also MapStateE2ETests): launches
/// <c>dotnet TarkovHelper.dll</c> (running the DLL bypasses the requireAdministrator
/// apphost manifest), points it at a throwaway Config folder via the
/// TARKOVHELPER_CONFIG_PATH environment variable, tracks the main window, and exposes
/// Win32 window control plus UI Automation for in-window controls (WPF surfaces
/// x:Name as the UIA AutomationId).
///
/// E2E tests need an interactive desktop and take a few seconds per launch; exclude
/// them from quick runs with <c>dotnet test --filter Category!=E2E</c>. They skip
/// automatically when the app build output is missing.
///
/// Coordinates: the test host's DPI awareness is forced to per-monitor-v2 up front
/// (see <see cref="TestHostDpiAwareness"/>) so GetWindowRect deterministically returns
/// physical pixels, and <see cref="GetWindowRect"/> normalizes them by the window's DPI
/// back to WPF device-independent units (verified on a 200% display). Without the
/// forcing, awareness silently depended on whether UI Automation had been touched
/// first, flipping the coordinate space between runs.
/// </summary>
internal sealed class AppDriver : IDisposable
{
    private readonly Process _process;
    private readonly IntPtr _hwnd;
    // Root UIA element for the main window, resolved once — every TryFindElement poll
    // reuses it instead of re-entering COM via FromHandle each 250ms tick.
    private readonly AutomationElement _uiaRoot;

    private AppDriver(Process process, IntPtr hwnd)
    {
        _process = process;
        _hwnd = hwnd;
        _uiaRoot = AutomationElement.FromHandle(hwnd);
    }

    public static AppDriver Launch(string configDir)
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
            return new AppDriver(process, WaitForMainWindow(process));
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

    #region Win32 window control

    /// <summary>
    /// The window rect in WPF device-independent units: raw physical pixels scaled by
    /// the window's own DPI (the same DPI WPF used to place it), so assertions hold on
    /// scaled displays.
    /// </summary>
    public Win32.WindowRect GetWindowRect()
    {
        Win32.GetWindowRect(_hwnd, out var rect);
        return new Win32.WindowRect(rect, 96.0 / Win32.GetDpiForWindow(_hwnd));
    }

    /// <summary>Off-screen check in raw physical units (same space as GetSystemMetrics).</summary>
    public bool IsWithinVirtualScreen()
    {
        Win32.GetWindowRect(_hwnd, out var rect);
        return Win32.IsWithinVirtualScreen(new Win32.WindowRect(rect, scale: 1.0));
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

    #endregion

    #region UI Automation

    /// <summary>
    /// Selects a main-window tab (a named RadioButton, e.g. "TabMap") and waits until
    /// <paramref name="readyElementAutomationId"/> — an element unique to the switched-in
    /// page — appears. A click that lands during the app's startup loading window is
    /// swallowed by MainWindow's _isLoading guard while still checking the radio button
    /// (so re-selecting it would no-op); the retry bounces through another tab to
    /// re-fire the Checked event once loading has finished.
    /// </summary>
    public void SelectTab(string tabAutomationId, string readyElementAutomationId,
        string bounceTabAutomationId = "TabItems")
    {
        if (string.Equals(tabAutomationId, bounceTabAutomationId, StringComparison.Ordinal))
            throw new ArgumentException(
                $"bounce tab must differ from the target tab '{tabAutomationId}' — bouncing to itself " +
                "just re-selects the checked radio button (a no-op) and would spin to the timeout",
                nameof(bounceTabAutomationId));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        Select(WaitForElement(tabAutomationId, deadline));

        while (TryFindElement(readyElementAutomationId) == null)
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"page for tab '{tabAutomationId}' did not appear within 60s");

            Thread.Sleep(250);
            Select(WaitForElement(bounceTabAutomationId, deadline));
            Thread.Sleep(250);
            Select(WaitForElement(tabAutomationId, deadline));
        }
    }

    /// <summary>
    /// Polls the combo box until its UIA selection is non-empty and returns the selected
    /// item's Name (for explicit ComboBoxItem items, their Content text).
    /// </summary>
    public string WaitForComboSelection(string comboAutomationId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        var combo = WaitForElement(comboAutomationId, deadline);

        while (DateTime.UtcNow < deadline)
        {
            var selection = ((SelectionPattern)combo.GetCurrentPattern(SelectionPattern.Pattern))
                .Current.GetSelection();
            if (selection.Length > 0)
            {
                var name = selection[0].Current.Name;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"combo '{comboAutomationId}' did not report a selection within 30s");
    }

    private static void Select(AutomationElement element)
        => ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

    private AutomationElement WaitForElement(string automationId, DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            var element = TryFindElement(automationId);
            if (element != null) return element;
            Thread.Sleep(250);
        }
        throw new TimeoutException($"element '{automationId}' did not appear in the main window");
    }

    private AutomationElement? TryFindElement(string automationId)
        => _uiaRoot.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));

    #endregion

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

/// <summary>Skips e2e tests when the app build output is not present.</summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (AppUnderTest.DllPath == null)
            Skip = "TarkovHelper build output not found - build TarkovHelper.csproj first";
    }
}

/// <summary>
/// All e2e test classes join this single xUnit collection so they run SERIALLY.
/// Without it, xUnit runs different classes in parallel by default, which would launch
/// two real app instances at once — they fight over window focus and the global
/// keyboard hook, and one class's Dispose calls the process-global
/// SqliteConnection.ClearAllPools under the other's in-flight DB access.
/// </summary>
[CollectionDefinition("E2E")]
public sealed class E2ETestCollection { }

/// <summary>
/// Shared per-class scaffolding for e2e tests: an isolated temp root for throwaway
/// Config folders, and cleanup that clears the process-wide SQLite pools first so the
/// user_data.db files are unlocked before the directory delete.
/// </summary>
public abstract class E2ETestBase : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "TarkovHelperE2E", Guid.NewGuid().ToString("N"));

    protected string NewConfigDir()
    {
        var dir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
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
        public WindowRect(RECT r, double scale)
        {
            Left = r.Left * scale;
            Top = r.Top * scale;
            Width = (r.Right - r.Left) * scale;
            Height = (r.Bottom - r.Top) * scale;
        }

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
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
}

/// <summary>
/// Pins the test host's DPI awareness before any test code runs. Loading UI Automation
/// (or other UI stacks) can flip an unset process to DPI-aware mid-run, which would
/// silently switch GetWindowRect between virtualized and physical coordinates depending
/// on test ordering — forcing per-monitor-v2 here makes the coordinate space
/// deterministic (AppDriver.GetWindowRect then converts physical px to WPF units).
/// </summary>
internal static class TestHostDpiAwareness
{
    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Ensure()
    {
        // Fails harmlessly when awareness is already set — by then it is aware anyway.
        Win32.SetProcessDpiAwarenessContext(PerMonitorAwareV2);
    }
}

/// <summary>
/// Direct user_data.db access for seeding settings before a launch and asserting the
/// persisted values after a close. The app's own schema creation is CREATE TABLE IF NOT
/// EXISTS, so it adopts a pre-created file and adds the remaining tables on startup.
/// </summary>
internal static class E2EDb
{
    /// <summary>Creates a minimal user_data.db so tests can seed settings without a first app launch.</summary>
    public static void CreateUserDataDb(string configDir)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(configDir, "user_data.db")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS UserSettings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)";
        command.ExecuteNonQuery();
    }

    public static void SeedSetting(string configDir, string key, string value)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(configDir, "user_data.db")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO UserSettings (Key, Value) VALUES ($key, $value) " +
            "ON CONFLICT(Key) DO UPDATE SET Value = $value";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    /// <summary>Reads a persisted setting value, or null when the key/db is missing.</summary>
    public static string? ReadSetting(string configDir, string key)
    {
        var dbPath = Path.Combine(configDir, "user_data.db");
        if (!File.Exists(dbPath)) return null;

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM UserSettings WHERE Key = $key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }
}
