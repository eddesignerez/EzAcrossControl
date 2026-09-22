using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace WindowsHost
{
    public enum AppTheme
    {
        System,
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.txt");

        public static void Initialize()
        {
            var theme = LoadThemePreference();
            ApplyTheme(theme);
        }

        public static AppTheme LoadThemePreference()
        {
            if (File.Exists(ConfigPath))
            {
                if (Enum.TryParse(File.ReadAllText(ConfigPath), out AppTheme savedTheme))
                {
                    return savedTheme;
                }
            }
            return AppTheme.System;
        }

        public static void SaveThemePreference(AppTheme theme)
        {
            File.WriteAllText(ConfigPath, theme.ToString());
            ApplyTheme(theme);
        }

        public static void ApplyTheme(AppTheme theme)
        {
            bool isDark = false;
            if (theme == AppTheme.System)
            {
                isDark = IsSystemDarkTheme();
            }
            else
            {
                isDark = theme == AppTheme.Dark;
            }

            var dict = new ResourceDictionary();
            dict.Source = new Uri(isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);
            
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        private static bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val != null)
                    {
                        return (int)val == 0;
                    }
                }
            }
            catch
            {
                // Ignore registry errors
            }
            return false; // Default to Light if cannot determine
        }
    }
}
