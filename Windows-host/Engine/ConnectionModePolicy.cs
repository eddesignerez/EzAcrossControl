namespace WindowsHost.Engine;

/// <summary>Resolves the two requested modes without changing either preference.</summary>
public static class ConnectionModePolicy
{
    public const string ConflictMessage = "Connection modes conflict; choose the same mode or Auto";

    public static ConnectionMode? Resolve(ConnectionMode host, ConnectionMode companion)
    {
        if (!Enum.IsDefined(host) || !Enum.IsDefined(companion)) return null;
        if (host == ConnectionMode.Auto) return companion;
        if (companion == ConnectionMode.Auto || companion == host) return host;
        return null;
    }

    public static bool TryParse(string? value, out ConnectionMode mode)
    {
        mode = value switch
        {
            "Auto" => ConnectionMode.Auto,
            "USB" => ConnectionMode.Usb,
            "Wi-Fi" => ConnectionMode.Network,
            _ => (ConnectionMode)(-1)
        };
        return Enum.IsDefined(mode);
    }
}
