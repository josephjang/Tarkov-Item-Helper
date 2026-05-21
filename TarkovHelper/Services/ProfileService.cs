using TarkovHelper.Models;
using TarkovHelper.Services.Logging;

namespace TarkovHelper.Services;

public sealed class ProfileService
{
    private static readonly ILogger _log = Log.For<ProfileService>();
    private static ProfileService? _instance;
    public static ProfileService Instance => _instance ??= new ProfileService();

    public const string PvpProfileId = "pvp";
    public const string PveProfileId = "pve";
    private const string SettingKey = "app.activeGameMode";

    private GameMode _activeGameMode = GameMode.PVP;
    private bool _isAutoDetected;

    public GameMode ActiveGameMode => _activeGameMode;
    public string ActiveProfileId => _activeGameMode == GameMode.PVE ? PveProfileId : PvpProfileId;
    public bool IsAutoDetected => _isAutoDetected;

    public event EventHandler<ProfileChangedEventArgs>? ActiveProfileChanged;

    private ProfileService()
    {
        EftRaidEventService.Instance.RaidEvent += OnRaidEvent;
    }

    public async Task InitializeAsync()
    {
        var saved = await UserDataDbService.Instance.GetSettingAsync(SettingKey);
        var mode = saved == "PVE" ? GameMode.PVE : GameMode.PVP;
        _log.Info($"Initialized: {mode}");

        // SettingsService and other singletons may already be constructed (default PVP)
        // before InitializeAsync runs. If the saved mode differs, fire the event so they
        // reload their profile-scoped state.
        if (mode != _activeGameMode)
        {
            _activeGameMode = mode;
            ActiveProfileChanged?.Invoke(this, new ProfileChangedEventArgs(mode, false));
        }
    }

    public void SetActiveGameMode(GameMode mode, bool isAuto = false)
    {
        if (mode == GameMode.Unknown) return;
        if (_activeGameMode == mode && _isAutoDetected == isAuto) return;

        _activeGameMode = mode;
        _isAutoDetected = isAuto;

        _ = UserDataDbService.Instance.SetSettingAsync(SettingKey, mode == GameMode.PVE ? "PVE" : "PVP");
        _log.Info($"Switched to {mode} (auto={isAuto})");

        ActiveProfileChanged?.Invoke(this, new ProfileChangedEventArgs(mode, isAuto));
    }

    public static string GetProfileId(GameMode mode) =>
        mode == GameMode.PVE ? PveProfileId : PvpProfileId;

    private void OnRaidEvent(object? sender, EftRaidEventArgs e)
    {
        if (e.EventType != EftRaidEventType.SessionModeDetected) return;
        var mode = EftRaidEventService.Instance.CurrentGameMode;
        if (mode == GameMode.PVP || mode == GameMode.PVE)
            SetActiveGameMode(mode, isAuto: true);
    }
}

public class ProfileChangedEventArgs : EventArgs
{
    public GameMode GameMode { get; }
    public bool IsAutoDetected { get; }

    public ProfileChangedEventArgs(GameMode mode, bool isAuto)
    {
        GameMode = mode;
        IsAutoDetected = isAuto;
    }
}
