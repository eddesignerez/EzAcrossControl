using System;
using System.IO;
using System.Text.Json;
using WindowsHost.V2.Edge;

namespace WindowsHost
{
    public class AppSettings
    {
        public int ServerPort { get; set; } = Config.DefaultPort;
        public EdgeOptions EdgeTransition { get; set; } = new EdgeOptions();
        public WindowsHost.Engine.ConnectionMode ConnectionMode { get; set; } = WindowsHost.Engine.ConnectionMode.Auto;
        // Only the public key is retained. The Android private key remains in Android Keystore.
        public CompanionPairing? CompanionPairing { get; set; }
        // Theme could be migrated here later
    }

    public class CompanionPairing
    {
        public string InstallationId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EZ Across Control", "config.json");

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
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
