using WindowsHost;

namespace WindowsHost.Tests;

public class LanPortPermissionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(3000)]
    [InlineData(4000)]
    [InlineData(5000)]
    [InlineData(5173)]
    public async Task InvalidOrReservedPortsNeverLaunchElevation(int port)
    {
        Assert.False(LanPortPermission.IsValidPort(port));
        Assert.False(await LanPortPermission.RequestAsync(port));
        Assert.Equal(2, LanPortPermission.Configure(port));
    }

    [Theory]
    [InlineData(8765)]
    [InlineData(8787)]
    [InlineData(1)]
    [InlineData(65535)]
    public void AllowedPortsPassValidation(int port) => Assert.True(LanPortPermission.IsValidPort(port));
}
