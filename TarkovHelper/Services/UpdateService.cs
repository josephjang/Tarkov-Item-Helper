using System.Net.Http;
using System.Reflection;
using System.Timers;
using System.Xml.Linq;
using TarkovHelper.Services.Logging;

namespace TarkovHelper.Services
{
    /// <summary>
    /// Service for checking and managing application updates
    /// </summary>
    public class UpdateService
    {
        private static readonly ILogger _log = Log.For<UpdateService>();
        private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
        public static UpdateService Instance => _instance.Value;

        internal const string UpdateXmlUrl = "https://raw.githubusercontent.com/josephjang/Tarkov-Item-Helper/main/update.xml";
        private const int CheckIntervalMinutes = 3;

        private readonly HttpClient _httpClient;
        private readonly System.Timers.Timer _checkTimer;
        private readonly Version _currentVersion;

        private bool _isChecking;
        private UpdateInfo? _availableUpdate;
        private DateTime? _lastCheckTime;

        /// <summary>
        /// Fired when update check is completed
        /// </summary>
        public event EventHandler<UpdateCheckEventArgs>? UpdateCheckCompleted;

        /// <summary>
        /// Fired when update check starts
        /// </summary>
        public event EventHandler? UpdateCheckStarted;

        /// <summary>
        /// Currently available update (null if no update available)
        /// </summary>
        public UpdateInfo? AvailableUpdate => _availableUpdate;

        /// <summary>
        /// Whether an update check is in progress
        /// </summary>
        public bool IsChecking => _isChecking;

        /// <summary>
        /// Current application version
        /// </summary>
        public Version CurrentVersion => _currentVersion;

        /// <summary>
        /// Last time update was checked
        /// </summary>
        public DateTime? LastCheckTime => _lastCheckTime;

        private UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

            _checkTimer = new System.Timers.Timer(TimeSpan.FromMinutes(CheckIntervalMinutes).TotalMilliseconds);
            _checkTimer.Elapsed += OnTimerElapsed;
            _checkTimer.AutoReset = true;
        }

        /// <summary>
        /// Start automatic update checking
        /// </summary>
        public void StartAutoCheck()
        {
            _log.Info($"Starting automatic update check (interval: {CheckIntervalMinutes} minutes)");
            _checkTimer.Start();

            // Do initial check immediately
            _ = CheckForUpdateAsync();
        }

        /// <summary>
        /// Stop automatic update checking
        /// </summary>
        public void StopAutoCheck()
        {
            _log.Info("Stopping automatic update check");
            _checkTimer.Stop();
        }

        /// <summary>
        /// Manually check for updates
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            if (_isChecking)
            {
                _log.Debug("Update check already in progress, skipping");
                return _availableUpdate;
            }

            _isChecking = true;
            UpdateCheckStarted?.Invoke(this, EventArgs.Empty);
            _log.Debug("Checking for updates...");

            try
            {
                var response = await _httpClient.GetStringAsync(UpdateXmlUrl);
                var updateInfo = ParseUpdateXml(response);

                if (updateInfo != null && updateInfo.Version > _currentVersion)
                {
                    _availableUpdate = updateInfo;
                    _log.Info($"Update available: {updateInfo.Version} (current: {_currentVersion})");
                }
                else
                {
                    _availableUpdate = null;
                    _log.Debug($"No update available (current: {_currentVersion}, latest: {updateInfo?.Version})");
                }

                _lastCheckTime = DateTime.Now;
                UpdateCheckCompleted?.Invoke(this, new UpdateCheckEventArgs(_availableUpdate, null));
                return _availableUpdate;
            }
            catch (Exception ex)
            {
                _log.Error("Failed to check for updates", ex);
                _lastCheckTime = DateTime.Now;
                UpdateCheckCompleted?.Invoke(this, new UpdateCheckEventArgs(null, ex));
                return null;
            }
            finally
            {
                _isChecking = false;
            }
        }

        /// <summary>
        /// Start the update download and installation process
        /// </summary>
        public void StartUpdate()
        {
            if (_availableUpdate == null)
            {
                _log.Warning("No update available to install");
                return;
            }

            _log.Info($"Starting update to version {_availableUpdate.Version}");

            // Use AutoUpdater.NET to handle the actual update
            AutoUpdaterDotNET.AutoUpdater.InstalledVersion = _currentVersion;
            AutoUpdaterDotNET.AutoUpdater.ShowSkipButton = false;
            AutoUpdaterDotNET.AutoUpdater.ShowRemindLaterButton = false;
            AutoUpdaterDotNET.AutoUpdater.Start(UpdateXmlUrl);
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            _ = CheckForUpdateAsync();
        }

        internal static UpdateInfo? ParseUpdateXml(string xmlContent)
        {
            try
            {
                var doc = XDocument.Parse(xmlContent);
                var item = doc.Root;

                // Root element is <item> directly
                if (item == null || item.Name.LocalName != "item")
                {
                    _log.Warning("Update XML does not have item as root element");
                    return null;
                }

                var versionStr = item.Element("version")?.Value;
                var url = item.Element("url")?.Value;
                var changelog = item.Element("changelog")?.Value;

                if (string.IsNullOrEmpty(versionStr) || string.IsNullOrEmpty(url))
                {
                    _log.Warning("Update XML missing required elements (version or url)");
                    return null;
                }

                if (!Version.TryParse(versionStr, out var version))
                {
                    _log.Warning($"Failed to parse version string: {versionStr}");
                    return null;
                }

                return new UpdateInfo
                {
                    Version = version,
                    DownloadUrl = url,
                    ChangelogUrl = changelog
                };
            }
            catch (Exception ex)
            {
                _log.Error("Failed to parse update XML", ex);
                return null;
            }
        }
    }

    /// <summary>
    /// Information about an available update
    /// </summary>
    public class UpdateInfo
    {
        public required Version Version { get; init; }
        public required string DownloadUrl { get; init; }
        public string? ChangelogUrl { get; init; }
    }

    /// <summary>
    /// Event args for update check completion
    /// </summary>
    public class UpdateCheckEventArgs : EventArgs
    {
        public UpdateInfo? UpdateInfo { get; }
        public Exception? Error { get; }
        public bool IsUpdateAvailable => UpdateInfo != null;

        public UpdateCheckEventArgs(UpdateInfo? updateInfo, Exception? error)
        {
            UpdateInfo = updateInfo;
            Error = error;
        }
    }
}
