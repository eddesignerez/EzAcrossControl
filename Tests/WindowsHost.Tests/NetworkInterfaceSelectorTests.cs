using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using Xunit;
using WindowsHost;
using System.Linq;

namespace WindowsHost.Tests
{
    public class NetworkInterfaceSelectorTests
    {
        private class MockNetworkInfo : INetworkInfo
        {
            public OperationalStatus Status { get; set; } = OperationalStatus.Up;
            public NetworkInterfaceType Type { get; set; } = NetworkInterfaceType.Ethernet;
            public bool HasIpv4Gateway { get; set; } = true;
            public List<IPAddress> Ipv4Addresses { get; set; } = new List<IPAddress>();

            IEnumerable<IPAddress> INetworkInfo.Ipv4Addresses => Ipv4Addresses;
        }

        [Fact]
        public void ScenarioA_IgnoreAPIPA_SelectValid()
        {
            var apipa = new MockNetworkInfo { Ipv4Addresses = { IPAddress.Parse("169.254.83.107") } };
            var valid = new MockNetworkInfo { Ipv4Addresses = { IPAddress.Parse("192.168.158.151") } };
            
            var result = NetworkInterfaceSelector.GetPreferredLanIPv4(new[] { apipa, valid });
            Assert.Equal(IPAddress.Parse("192.168.158.151"), result.Address);
        }

        [Fact]
        public void ScenarioB_IgnoreLoopback_SelectValid()
        {
            var loopback = new MockNetworkInfo { Type = NetworkInterfaceType.Loopback, Ipv4Addresses = { IPAddress.Parse("127.0.0.1") } };
            var valid = new MockNetworkInfo { Ipv4Addresses = { IPAddress.Parse("10.0.0.15") } };
            
            var result = NetworkInterfaceSelector.GetPreferredLanIPv4(new[] { loopback, valid });
            Assert.Equal(IPAddress.Parse("10.0.0.15"), result.Address);
        }

        [Fact]
        public void ScenarioC_IgnoreDown_SelectUp()
        {
            var down = new MockNetworkInfo { Status = OperationalStatus.Down, Ipv4Addresses = { IPAddress.Parse("192.168.1.10") } };
            var up = new MockNetworkInfo { Status = OperationalStatus.Up, Ipv4Addresses = { IPAddress.Parse("192.168.2.20") } };
            
            var result = NetworkInterfaceSelector.GetPreferredLanIPv4(new[] { down, up });
            Assert.Equal(IPAddress.Parse("192.168.2.20"), result.Address);
        }

        [Fact]
        public void ScenarioD_PreferWithGateway()
        {
            var noGateway = new MockNetworkInfo { Type = NetworkInterfaceType.Tunnel, HasIpv4Gateway = false, Ipv4Addresses = { IPAddress.Parse("192.168.56.1") } };
            var hasGateway = new MockNetworkInfo { Type = NetworkInterfaceType.Ethernet, HasIpv4Gateway = true, Ipv4Addresses = { IPAddress.Parse("192.168.1.100") } };
            
            var result = NetworkInterfaceSelector.GetPreferredLanIPv4(new[] { noGateway, hasGateway });
            Assert.Equal(IPAddress.Parse("192.168.1.100"), result.Address);
        }

        [Fact]
        public void ScenarioE_NoValidIPv4_ReturnsNull()
        {
            var apipa = new MockNetworkInfo { Ipv4Addresses = { IPAddress.Parse("169.254.83.107") } };
            var down = new MockNetworkInfo { Status = OperationalStatus.Down, Ipv4Addresses = { IPAddress.Parse("192.168.1.10") } };
            var loopback = new MockNetworkInfo { Type = NetworkInterfaceType.Loopback, Ipv4Addresses = { IPAddress.Parse("127.0.0.1") } };
            
            var result = NetworkInterfaceSelector.GetPreferredLanIPv4(new[] { apipa, down, loopback });
            Assert.Null(result.Address);
        }
    }
}
