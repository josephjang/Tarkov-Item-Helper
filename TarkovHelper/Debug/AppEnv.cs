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
    }
}
