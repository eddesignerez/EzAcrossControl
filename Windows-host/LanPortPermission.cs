using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using Microsoft.Win32;

namespace WindowsHost;

public static class LanPortPermission
{
    public const string RuleName = "EZ Across Control LAN";
    private const string RegistryPath = @"Software\ElementZero\EZAcrossControl";

    public static bool IsValidPort(int port) => port is >= 1 and <= 65535
        && port is not (3000 or 4000 or 5000 or 5173);

    public static bool HasFirewallRule(int port, string executable)
    {
        try
        {
            dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
            dynamic rule = policy.Rules.Item(RuleName);
            return rule.Enabled && rule.Direction == 1 && rule.Action == 1 && rule.Protocol == 6
                && rule.Profiles == 2 && rule.RemoteAddresses.Equals("LocalSubnet", StringComparison.OrdinalIgnoreCase)
                && rule.LocalPorts == port.ToString()
                && string.Equals(rule.ApplicationName, executable, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static async Task<bool> RequestAsync(int port)
    {
        if (!IsValidPort(port)) return false;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                Arguments = $"--configure-lan-port {port}", UseShellExecute = true,
                Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return false; }
    }

    // Runs only in the short-lived elevated process, without opening a window.
    public static int Configure(int port)
    {
        if (!IsValidPort(port)) return 2;
        try
        {
            var executable = Environment.ProcessPath!;
            dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
            dynamic rule;
            bool existing = false;
            try { rule = policy.Rules.Item(RuleName); existing = true; }
            catch { rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")!)!; }
            // Do not replace a rule belonging to a different installation.
            if (existing && !string.Equals(rule.ApplicationName, executable, StringComparison.OrdinalIgnoreCase)) return 3;

            using var command = new Process();
            command.StartInfo = new ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory, "netsh.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "http", "show", "urlacl", $"url=http://+:{port}/" })
                command.StartInfo.ArgumentList.Add(argument);
            command.Start();
            var reservation = command.StandardOutput.ReadToEnd();
            command.StandardError.ReadToEnd();
            command.WaitForExit();
            if (command.ExitCode != 0 || !reservation.Contains($"http://+:{port}/", StringComparison.OrdinalIgnoreCase))
            {
                command.StartInfo.ArgumentList.Clear();
                foreach (var argument in new[] { "http", "add", "urlacl", $"url=http://+:{port}/", "sddl=D:(A;;GX;;;IU)" })
                    command.StartInfo.ArgumentList.Add(argument);
                command.Start();
                command.StandardOutput.ReadToEnd();
                command.StandardError.ReadToEnd();
                command.WaitForExit();
                if (command.ExitCode != 0) return 4;
                using var key = Registry.LocalMachine.CreateSubKey(RegistryPath);
                key.SetValue($"OwnsUrlAcl{port}", 1, RegistryValueKind.DWord);
            }

            rule.Name = RuleName;
            rule.ApplicationName = executable;
            rule.Direction = 1;
            rule.Action = 1;
            rule.Protocol = 6;
            rule.LocalPorts = port.ToString();
            rule.RemoteAddresses = "LocalSubnet";
            rule.Profiles = 2; // Private networks only.
            rule.Enabled = true;
            if (!existing) policy.Rules.Add(rule);
            return HasFirewallRule(port, executable) ? 0 : 5;
        }
        catch { return 1; }
    }
}
