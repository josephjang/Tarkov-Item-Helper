using TarkovHelper.Services.Logging;

namespace TarkovHelper.Services.Settings;

/// <summary>
/// Quest-tab UI state persisted across app restarts (pattern: <see cref="MapSettings"/>):
/// the filter bar (Kappa/Item-Req checkboxes, trader, map, status), the detail-panel
/// width, and the recommendations-expander state. Search text is deliberately NOT
/// persisted — it is a transient query, and restoring it would surprise more than help
/// (see feature-quest-overview-filters.md).
///
/// First access must happen after UserDataDbService is initialized (the page touches
/// this from Loaded, never from a constructor — see the init-order note on
/// QuestListPage.RestoreFilterSettings).
/// </summary>
public class QuestListSettings
{
    private static readonly ILogger _log = Log.For<QuestListSettings>();
    private static QuestListSettings? _instance;
    public static QuestListSettings Instance => _instance ??= new QuestListSettings();

    private readonly UserDataDbService _userDataDb = UserDataDbService.Instance;
    private bool _settingsLoaded;

    #region Setting Keys

    private const string KeyKappaOnly = "questList.kappaOnly";
    private const string KeyItemRequired = "questList.itemRequired";
    private const string KeyTrader = "questList.trader";
    private const string KeyMap = "questList.map";
    private const string KeyStatusTag = "questList.statusTag";
    private const string KeyDetailPanelWidth = "questList.detailPanelWidth";
    private const string KeyRecommendationsExpanded = "questList.recommendationsExpanded";

    #endregion

    #region Constants

    /// <summary>Matches the XAML default of the detail column (QuestListPage.xaml, DetailColumn).</summary>
    public const double DefaultDetailPanelWidth = 350;
    public const double MinDetailPanelWidth = 250;
    public const double MaxDetailPanelWidth = 800;

    /// <summary>Matches the page's initial CmbStatus selection ("Active").</summary>
    public const string DefaultStatusTag = "Active";

    #endregion

    #region Cached Values

    private bool? _kappaOnly;
    private bool? _itemRequired;
    private string? _trader;
    private string? _map;
    private string? _statusTag;
    private double? _detailPanelWidth;
    private bool? _recommendationsExpanded;

    #endregion

    private QuestListSettings()
    {
        LoadSettings();
    }

    public bool KappaOnly
    {
        get
        {
            EnsureLoaded();
            return _kappaOnly ?? false;
        }
        set
        {
            if (_kappaOnly != value)
            {
                _kappaOnly = value;
                SaveSetting(KeyKappaOnly, value.ToString());
            }
        }
    }

    public bool ItemRequired
    {
        get
        {
            EnsureLoaded();
            return _itemRequired ?? false;
        }
        set
        {
            if (_itemRequired != value)
            {
                _itemRequired = value;
                SaveSetting(KeyItemRequired, value.ToString());
            }
        }
    }

    /// <summary>The trader combo's Tag value; empty string = "All Traders".</summary>
    public string Trader
    {
        get
        {
            EnsureLoaded();
            return _trader ?? "";
        }
        set
        {
            if (_trader != value)
            {
                _trader = value;
                SaveSetting(KeyTrader, value ?? "");
            }
        }
    }

    /// <summary>The map combo's Tag value (normalized map name); empty string = "All Maps".</summary>
    public string Map
    {
        get
        {
            EnsureLoaded();
            return _map ?? "";
        }
        set
        {
            if (_map != value)
            {
                _map = value;
                SaveSetting(KeyMap, value ?? "");
            }
        }
    }

    /// <summary>The status combo's Tag value ("Active", "All", "Locked", "Done", "Failed", "Unavailable").</summary>
    public string StatusTag
    {
        get
        {
            EnsureLoaded();
            return string.IsNullOrEmpty(_statusTag) ? DefaultStatusTag : _statusTag;
        }
        set
        {
            if (_statusTag != value)
            {
                _statusTag = value;
                SaveSetting(KeyStatusTag, value ?? DefaultStatusTag);
            }
        }
    }

    public double DetailPanelWidth
    {
        get
        {
            EnsureLoaded();
            return _detailPanelWidth ?? DefaultDetailPanelWidth;
        }
        set
        {
            var clampedValue = Math.Clamp(value, MinDetailPanelWidth, MaxDetailPanelWidth);
            if (Math.Abs((_detailPanelWidth ?? DefaultDetailPanelWidth) - clampedValue) > 1)
            {
                _detailPanelWidth = clampedValue;
                SaveSetting(KeyDetailPanelWidth, SettingsValue.FormatDouble(clampedValue));
            }
        }
    }

    public bool RecommendationsExpanded
    {
        get
        {
            EnsureLoaded();
            return _recommendationsExpanded ?? false;
        }
        set
        {
            if (_recommendationsExpanded != value)
            {
                _recommendationsExpanded = value;
                SaveSetting(KeyRecommendationsExpanded, value.ToString());
            }
        }
    }

    #region Private Methods

    private void EnsureLoaded()
    {
        if (!_settingsLoaded) LoadSettings();
    }

    private void SaveSetting(string key, string value)
    {
        try
        {
            _userDataDb.SetSetting(key, value);
        }
        catch (Exception ex)
        {
            _log.Error($"Save failed: {ex.Message}");
        }
    }

    private void LoadSettings()
    {
        _settingsLoaded = true;

        try
        {
            if (bool.TryParse(_userDataDb.GetSetting(KeyKappaOnly), out var kappaOnly))
                _kappaOnly = kappaOnly;

            if (bool.TryParse(_userDataDb.GetSetting(KeyItemRequired), out var itemRequired))
                _itemRequired = itemRequired;

            _trader = _userDataDb.GetSetting(KeyTrader);
            _map = _userDataDb.GetSetting(KeyMap);
            _statusTag = _userDataDb.GetSetting(KeyStatusTag);

            if (SettingsValue.TryParseDouble(_userDataDb.GetSetting(KeyDetailPanelWidth), out var width))
                _detailPanelWidth = Math.Clamp(width, MinDetailPanelWidth, MaxDetailPanelWidth);

            if (bool.TryParse(_userDataDb.GetSetting(KeyRecommendationsExpanded), out var recommendationsExpanded))
                _recommendationsExpanded = recommendationsExpanded;
        }
        catch (Exception ex)
        {
            _log.Error($"Load failed: {ex.Message}");
        }
    }

    #endregion
}
