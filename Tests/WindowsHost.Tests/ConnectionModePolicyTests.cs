using WindowsHost.Engine;

namespace WindowsHost.Tests;

public class ConnectionModePolicyTests
{
    [Theory]
    [InlineData(ConnectionMode.Auto, ConnectionMode.Auto, ConnectionMode.Auto)]
    [InlineData(ConnectionMode.Auto, ConnectionMode.Usb, ConnectionMode.Usb)]
    [InlineData(ConnectionMode.Auto, ConnectionMode.Network, ConnectionMode.Network)]
    [InlineData(ConnectionMode.Usb, ConnectionMode.Auto, ConnectionMode.Usb)]
    [InlineData(ConnectionMode.Network, ConnectionMode.Auto, ConnectionMode.Network)]
    [InlineData(ConnectionMode.Usb, ConnectionMode.Usb, ConnectionMode.Usb)]
    [InlineData(ConnectionMode.Network, ConnectionMode.Network, ConnectionMode.Network)]
    [InlineData(ConnectionMode.Usb, ConnectionMode.Network, null)]
    [InlineData(ConnectionMode.Network, ConnectionMode.Usb, null)]
    public void ResolvesModesWithoutChangingTheHostPreference(
        ConnectionMode host, ConnectionMode companion, ConnectionMode? expected)
    {
        var settings = new AppSettings { ConnectionMode = host };
        Assert.Equal(expected, ConnectionModePolicy.Resolve(settings.ConnectionMode, companion));
        Assert.Equal(host, settings.ConnectionMode);
    }

    [Theory]
    [InlineData("Auto", ConnectionMode.Auto)]
    [InlineData("USB", ConnectionMode.Usb)]
    [InlineData("Wi-Fi", ConnectionMode.Network)]
    public void AcceptsProtocolModeNames(string value, ConnectionMode expected)
    {
        Assert.True(ConnectionModePolicy.TryParse(value, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Network")]
    [InlineData("invalid")]
    public void UnknownModesFailClosed(string? value)
    {
        Assert.False(ConnectionModePolicy.TryParse(value, out _));
        Assert.Null(ConnectionModePolicy.Resolve((ConnectionMode)999, ConnectionMode.Auto));
        Assert.Null(ConnectionModePolicy.Resolve(ConnectionMode.Auto, (ConnectionMode)999));
    }

    [Theory]
    [InlineData(ConnectionMode.Usb, ConnectionMode.Network)]
    [InlineData(ConnectionMode.Network, ConnectionMode.Usb)]
    public void ManualConflictBlocksSelectionEvenWhenBothTransportsAreAvailable(
        ConnectionMode host, ConnectionMode companion)
    {
        var device = new AndroidDevice { DeviceId = "tablet", UsbSerial = "tablet-usb", TcpSerial = "192.0.2.13:40000" };
        var effective = ConnectionModePolicy.Resolve(host, companion);
        var selectedSerial = effective.HasValue ? device.GetSerial(effective.Value) : null;
        Assert.Null(selectedSerial);
        Assert.Equal(ConnectionMode.Usb, ConnectionModePolicy.Resolve(ConnectionMode.Usb, ConnectionMode.Auto));
        Assert.Equal(ConnectionMode.Network, ConnectionModePolicy.Resolve(ConnectionMode.Auto, ConnectionMode.Network));
    }
}
