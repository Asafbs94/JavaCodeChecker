using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace JavaCodeChecker.Common
{

    // these are helper methods to get and save information from app.config file

    public static class ConfigUtil
    {
        public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> source)
        {
            return source ?? Enumerable.Empty<T>();
        }

        public static string GetSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public static void SetSetting(string key, string value)
        {
            Configuration configuration =
                ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            configuration.AppSettings.Settings[key].Value = value;

            configuration.Save(ConfigurationSaveMode.Full, true);

            ConfigurationManager.RefreshSection("appSettings");
        }
    }
}