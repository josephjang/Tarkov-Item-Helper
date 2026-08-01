using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TarkovHelper.Debug
{
    public static class AppEnv
    {
        #if DEBUG
        public static bool IsDebugMode { get; set; } = true;
        #else
        public static bool IsDebugMode { get; set; } = false;
        #endif

        public static string DataPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data");
        public static string CachePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");

        /// <summary>
        /// User-data location (user_data.db etc.). Overridable via the
        /// TARKOVHELPER_CONFIG_PATH environment variable so e2e tests can point the
        /// app at a throwaway folder instead of the real Config next to the exe.
        /// </summary>
        public static string ConfigPath { get; set; } =
            Environment.GetEnvironmentVariable("TARKOVHELPER_CONFIG_PATH")
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

        /// <summary>
        /// True when the TARKOVHELPER_DISABLE_DB_UPDATE environment variable is set
        /// (any non-empty value). E2e tests set it so the launched app never downloads
        /// a newer tarkov_data.db over the build-output Assets copy mid-test — the
        /// tests derive their expectations from a static copy of that same DB, and a
        /// background update would make the two silently diverge.
        /// </summary>
        public static bool DisableDbUpdate { get; } =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TARKOVHELPER_DISABLE_DB_UPDATE"));
    }
}
