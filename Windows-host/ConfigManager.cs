using System;
using System.IO;
using System.Text.Json;
using WindowsHost.Input.Edge;

namespace WindowsHost
{
    public class AppSettings
    {
        public EdgeTransitionOptions EdgeTransition { get; set; } = new EdgeTransitionOptions();
        // Theme could be migrated here later
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                        return settings;
                }
            }
            catch
            {
                // Ignore errors, return default
            }

            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
