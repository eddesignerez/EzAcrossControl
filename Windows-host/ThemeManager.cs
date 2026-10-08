using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Application = System.Windows.Application;

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
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EZ Across Control", "theme.txt");

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
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, theme.ToString());
            ApplyTheme(theme);
        }

        public static void ApplyTheme(AppTheme theme)
        {
            bool isDark = IsDarkTheme(theme);

            var dict = new ResourceDictionary();
            dict.Source = new Uri(isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);
            
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        public static bool IsDarkTheme(AppTheme theme) =>
            theme == AppTheme.Dark || (theme == AppTheme.System && IsSystemDarkTheme());

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
