using WindowsHost.Engine;

namespace WindowsHost.Tests;

public class AndroidDeviceManagerTests
{
    [Fact]
    public void ParseDevices_MergesUsbAndWifiButExcludesOfflineAndUnauthorized()
    {
        var result = AndroidDeviceManager.ParseDevices("List of devices attached\nusb-id\tdevice product:test model:Phone device:test\n192.0.2.12:37123\tdevice product:test model:Phone device:test\noffline-id\toffline model:Other\nlocked-id\tunauthorized model:Locked\n");
        var device = Assert.Single(result);
        Assert.Equal("usb-id", device.GetSerial(ConnectionMode.Auto));
        Assert.Equal("192.0.2.12:37123", device.GetSerial(ConnectionMode.Network));
        Assert.Equal("usb-id", device.GetSerial(ConnectionMode.Usb));
    }

    [Fact]
    public void ParseDevices_MdnsTlsSerialIsWifiNotUsb()
    {
        var device = Assert.Single(AndroidDeviceManager.ParseDevices("adb-test._adb-tls-connect._tcp\tdevice model:Phone\n"));
        Assert.False(device.UsbAvailable);
        Assert.True(device.NetworkAvailable);
        Assert.Equal(device.TcpSerial, device.GetSerial(ConnectionMode.Auto));
    }

    [Fact]
    public void ExplicitWifiNeverFallsBackToUsb()
    {
        var device = new AndroidDevice { DeviceId = "test", UsbSerial = "usb-id" };
        Assert.Null(device.GetSerial(ConnectionMode.Network));
    }

    [Fact]
    public void ExplicitUsbNeverFallsBackToWifi()
    {
        var device = new AndroidDevice { DeviceId = "test", TcpSerial = "192.0.2.12:37123" };
        Assert.Null(device.GetSerial(ConnectionMode.Usb));
    }

    [Fact]
    public void AutoFollowsUsbRemovalAndReconnection()
    {
        var both = Assert.Single(AndroidDeviceManager.ParseDevices("usb-id device model:Phone\n192.0.2.12:37123 device model:Phone\n"));
        Assert.Equal("usb-id", both.GetSerial(ConnectionMode.Auto));
        var unplugged = Assert.Single(AndroidDeviceManager.ParseDevices("usb-id offline model:Phone\n192.0.2.12:37123 device model:Phone\n"));
        Assert.Equal("192.0.2.12:37123", unplugged.GetSerial(ConnectionMode.Auto));
        var reconnected = Assert.Single(AndroidDeviceManager.ParseDevices("usb-id device model:Phone\n192.0.2.12:37123 device model:Phone\n"));
        Assert.Equal("usb-id", reconnected.GetSerial(ConnectionMode.Auto));
    }

    [Fact]
    public void WirelessDiscoveryOnlyUsesConnectionServicesOnCompanionAddress()
    {
        var services = "phone _adb-tls-connect._tcp 192.0.2.12:37123\nother _adb-tls-connect._tcp 192.0.2.13:40000\npair _adb-tls-pairing._tcp 192.0.2.12:41000\nbad _adb-tls-connect._tcp invalid\n";
        Assert.Equal(new[] { "192.0.2.12:37123" }, AndroidDeviceManager.GetWirelessEndpoints(services, "192.0.2.12"));
    }

    [Fact]
    public void WifiSelectsTabletThatConnectedApkEvenWithAnotherAndroidOnline()
    {
        var devices = AndroidDeviceManager.ParseDevices("phone-usb device model:Phone\n192.0.2.12:37123 device model:Phone\n192.0.2.13:40000 device model:Tablet\n");
        var tablet = AndroidDeviceManager.FindCompanionDevice(devices, ConnectionMode.Network,
            "Tablet", "192.0.2.13");
        Assert.Equal("192.0.2.13:40000", tablet?.GetSerial(ConnectionMode.Network));
        Assert.Same(tablet, AndroidDeviceManager.FindCompanionDevice(devices, ConnectionMode.Auto,
            "Tablet", "192.0.2.13"));
    }

    [Fact]
    public void ApkForTabletNeverSelectsOnlyUnrelatedPhone()
    {
        var devices = AndroidDeviceManager.ParseDevices("192.0.2.12:37123 device model:Phone\n");
        Assert.Null(AndroidDeviceManager.FindCompanionDevice(devices, ConnectionMode.Network,
            "Tablet", "192.0.2.13"));
    }

    [Fact]
    public void AutoPrefersUsbOfMatchingTabletDespitePhoneWifi()
    {
        var devices = AndroidDeviceManager.ParseDevices("tablet-usb device model:Tablet\n192.0.2.13:40000 device model:Tablet\n192.0.2.12:37123 device model:Phone\n");
        var tablet = AndroidDeviceManager.FindCompanionDevice(devices, ConnectionMode.Auto,
            "Tablet", "192.0.2.13");
        Assert.Equal("tablet-usb", tablet?.GetSerial(ConnectionMode.Auto));
    }
}
