using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WindowsHost
{
    public interface INetworkInfo
    {
        OperationalStatus Status { get; }
        NetworkInterfaceType Type { get; }
        bool HasIpv4Gateway { get; }
        IEnumerable<IPAddress> Ipv4Addresses { get; }
    }

    public class RealNetworkInfo : INetworkInfo
    {
        private readonly NetworkInterface _ni;

        public RealNetworkInfo(NetworkInterface ni)
        {
            _ni = ni;
        }

        public OperationalStatus Status => _ni.OperationalStatus;
        public NetworkInterfaceType Type => _ni.NetworkInterfaceType;
        
        public bool HasIpv4Gateway
        {
            get
            {
                try
                {
                    return _ni.GetIPProperties().GatewayAddresses
                        .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                }
                catch { return false; }
            }
        }

        public IEnumerable<IPAddress> Ipv4Addresses
        {
            get
            {
                try
                {
                    return _ni.GetIPProperties().UnicastAddresses
                        .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(u => u.Address);
                }
                catch { return Enumerable.Empty<IPAddress>(); }
            }
        }
    }

    public static class NetworkInterfaceSelector
    {
        public static (IPAddress? Address, string Diagnostics) GetPreferredLanIPv4(IEnumerable<INetworkInfo>? interfaces = null)
        {
            var logs = new List<string>();
            
            if (interfaces == null)
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Select(ni => new RealNetworkInfo(ni));
            }

            IPAddress? bestIp = null;
            bool bestHasGateway = false;
            NetworkInterfaceType? bestType = null;

            foreach (var ni in interfaces)
            {
                logs.Add($"[NETWORK] Candidate interface: {ni.Type} (Status: {ni.Status})");
                
                if (ni.Status != OperationalStatus.Up)
                {
                    logs.Add($"[NETWORK] Rejected reason: Down");
                    continue;
                }

                if (ni.Type == NetworkInterfaceType.Loopback)
                {
                    logs.Add($"[NETWORK] Rejected reason: Loopback");
                    continue;
                }

                bool hasGateway = ni.HasIpv4Gateway;
                logs.Add($"[NETWORK] Gateway: {hasGateway}");

                foreach (var ip in ni.Ipv4Addresses)
                {
                    var ipBytes = ip.GetAddressBytes();
                    logs.Add($"[NETWORK] IPv4: {ip}");
                    
                    if (ipBytes[0] == 127)
                    {
                        logs.Add($"[NETWORK] Rejected reason: Loopback IP");
                        continue;
                    }
                    
                    if (ipBytes[0] == 169 && ipBytes[1] == 254)
                    {
                        logs.Add($"[NETWORK] Rejected reason: APIPA");
                        continue;
                    }

                    if (bestIp == null)
                    {
                        bestIp = ip;
                        bestHasGateway = hasGateway;
                        bestType = ni.Type;
                    }
                    else if (hasGateway && !bestHasGateway)
                    {
                        bestIp = ip;
                        bestHasGateway = hasGateway;
                        bestType = ni.Type;
                    }
                    else if (hasGateway == bestHasGateway)
                    {
                        // prefer Ethernet or Wireless over others if same gateway status
                        bool isCurrentPhysical = (ni.Type == NetworkInterfaceType.Ethernet || ni.Type == NetworkInterfaceType.Wireless80211);
                        bool isBestPhysical = (bestType == NetworkInterfaceType.Ethernet || bestType == NetworkInterfaceType.Wireless80211);
                        
                        if (isCurrentPhysical && !isBestPhysical)
                        {
                            bestIp = ip;
                            bestType = ni.Type;
                        }
                    }
                }
            }

            if (bestIp != null)
            {
                logs.Add($"[NETWORK] Selected LAN IPv4: {bestIp}");
            }
            else
            {
                logs.Add($"[NETWORK] Selected LAN IPv4: NONE");
            }

            return (bestIp, string.Join("\n", logs));
        }
    }
}
