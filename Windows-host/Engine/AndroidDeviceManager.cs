using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WindowsHost.Engine
{
    public class AndroidDeviceManager
    {
        public async Task<List<AndroidDevice>> GetDevicesAsync()
        {
            var output = await RunAdbCommandAsync("devices -l");
            return ParseDevices(output);
        }

        public static List<AndroidDevice> ParseDevices(string output)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            var rawDevices = new List<(string Serial, string Model, bool IsTcp)>();

            foreach (var line in lines)
            {
                if (line.StartsWith("List of devices")) continue;
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[1] == "device")
                {
                    string serial = parts[0];
                    string model = "Unknown";
                    
                    var modelMatch = Regex.Match(line, @"model:(\S+)");
                    if (modelMatch.Success)
                    {
                        model = modelMatch.Groups[1].Value;
                    }
                    else
                    {
                        var deviceMatch = Regex.Match(line, @"device:(\S+)");
                        if (deviceMatch.Success) model = deviceMatch.Groups[1].Value;
                    }

                    bool isTcp = serial.Contains(":")
                        || serial.Contains("._adb-tls-connect._tcp", StringComparison.OrdinalIgnoreCase);
                    rawDevices.Add((serial, model, isTcp));
                }
            }

            var devices = new List<AndroidDevice>();
            
            // Group by model to merge USB and TCP representations of the same device
            var grouped = rawDevices.GroupBy(d => d.Model);
            foreach (var group in grouped)
            {
                var device = new AndroidDevice
                {
                    DeviceId = group.Key,
                    Model = group.Key
                };

                var usb = group.FirstOrDefault(d => !d.IsTcp);
                if (usb.Serial != null) device.UsbSerial = usb.Serial;

                var tcp = group.FirstOrDefault(d => d.IsTcp);
                if (tcp.Serial != null) device.TcpSerial = tcp.Serial;
                
                devices.Add(device);
            }

            return devices;
        }

        public static IEnumerable<string> GetWirelessEndpoints(string services, string companionAddress)
        {
            if (!IPAddress.TryParse(companionAddress, out var companion)) yield break;
            foreach (var line in services.Split('\n'))
            {
                if (!line.Contains("_adb-tls-connect._tcp", StringComparison.OrdinalIgnoreCase)) continue;
                var endpoint = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (endpoint == null || !IPEndPoint.TryParse(endpoint, out var parsed)) continue;
                if (parsed.Address.Equals(companion) && parsed.Port > 0) yield return endpoint;
            }
        }

        public static AndroidDevice? FindCompanionDevice(IEnumerable<AndroidDevice> devices,
            ConnectionMode mode, string? companionName, string? companionAddress)
        {
            var candidates = devices.Where(d => d.GetSerial(mode) != null).ToList();
            bool nameMatches(AndroidDevice device) => !string.IsNullOrWhiteSpace(companionName)
                && string.Equals(device.Model, companionName, StringComparison.OrdinalIgnoreCase);

            if (IPAddress.TryParse(companionAddress, out var address))
            {
                var matchingNetwork = candidates.FirstOrDefault(d => d.TcpSerial != null
                    && IPEndPoint.TryParse(d.TcpSerial, out var endpoint)
                    && endpoint.Address.Equals(address));
                if (matchingNetwork != null) return matchingNetwork;
            }

            var matchingName = candidates.FirstOrDefault(nameMatches);
            if (matchingName != null) return matchingName;

            // A connected APK must never silently start scrcpy on an unrelated ADB device.
            return string.IsNullOrWhiteSpace(companionName) && string.IsNullOrWhiteSpace(companionAddress)
                && candidates.Count == 1 ? candidates[0] : null;
        }

        public async Task DiscoverNetworkAsync(string? companionAddress, AndroidDevice? usbDevice)
        {
            if (string.IsNullOrEmpty(companionAddress) || !IPAddress.TryParse(companionAddress, out var address)) return;
            var services = await RunAdbCommandAsync("mdns services");
            foreach (var endpoint in GetWirelessEndpoints(services, companionAddress).Distinct())
                await ConnectTcpAsync(endpoint);

            // Paired wireless debugging may be active even when mDNS is blocked
            // by the LAN. Read its current port through the authorized USB device.
            if (usbDevice?.UsbSerial != null)
            {
                var portText = await RunAdbCommandAsync($"-s {usbDevice.UsbSerial} shell getprop service.adb.tls.port");
                if (int.TryParse(portText.Trim(), out int port) && port > 0 && port <= 65535)
                    await ConnectTcpAsync(new IPEndPoint(address, port).ToString());
            }
        }

        public async Task<bool> ConnectTcpAsync(string ipAndPort)
        {
            var output = await RunAdbCommandAsync($"connect {ipAndPort}");
            return output.TrimStart().StartsWith("connected to", StringComparison.OrdinalIgnoreCase)
                || output.TrimStart().StartsWith("already connected to", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> DisconnectTcpAsync(string ipAndPort)
        {
            var output = await RunAdbCommandAsync($"disconnect {ipAndPort}");
            return output.Contains("disconnected");
        }

        private async Task<string> RunAdbCommandAsync(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(5000));
                    
                    if (!process.HasExited)
                    {
                        process.Kill();
                        return string.Empty;
                    }
                    
                    var output = await outputTask;
                    var errors = await errorTask;
                    return process.ExitCode == 0 ? output : output + errors;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
