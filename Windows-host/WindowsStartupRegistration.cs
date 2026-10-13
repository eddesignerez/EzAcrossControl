using Microsoft.Win32;
using System.IO;

namespace WindowsHost;

public interface IStartupRegistrationStore
{
    string? Read();
    void Write(string? command);
}

/// <summary>Owns only this application's entry in the current user's startup list.</summary>
public sealed class WindowsStartupRegistration
{
    private readonly IStartupRegistrationStore _store;
    private readonly string _command;

    public WindowsStartupRegistration(string executablePath, IStartupRegistrationStore? store = null)
    {
        _command = BuildCommand(executablePath);
        _store = store ?? new CurrentUserRunStore();
    }

    public static string BuildCommand(string executablePath)
    {
        if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"')
            || !string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An absolute executable path is required", nameof(executablePath));
        var command = '"' + executablePath + '"';
        if (command.Length > 260) throw new ArgumentException("Windows startup command is too long", nameof(executablePath));
        return command;
    }

    public bool IsEnabled => string.Equals(_store.Read(), _command, StringComparison.OrdinalIgnoreCase);
    public void SetEnabled(bool enabled) => _store.Write(enabled ? _command : null);

    private sealed class CurrentUserRunStore : IStartupRegistrationStore
    {
        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "EZAcrossControl";

        public string? Read()
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) as string;
        }

        public void Write(string? command)
        {
            if (command == null)
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            else
            {
                using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
        }
    }
}
