using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Goggles3.Multi.Windows;

internal sealed record UsbAdapter(string Id, string Label, int Index, IPAddress? Address, uint Problem);

internal static class Adapters
{
    internal static readonly string[] Pool = ["192.168.60.1", "192.168.60.3", "192.168.60.4", "192.168.60.5"];
    public static UsbAdapter[] Find()
    {
        const string key = @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}";
        const string prefix = @"USB\VID_2CA3&PID_0020";
        var adapters = NetworkInterface.GetAllNetworkInterfaces();
        var addresses = adapters.SelectMany(n => n.GetIPProperties().UnicastAddresses.Select(a => a.Address.ToString())).ToArray();
        var result = new List<UsbAdapter>();
        foreach (var adapter in adapters)
        {
            string id = adapter.Id.Trim('{', '}');
            using var connection = Registry.LocalMachine.OpenSubKey($@"{key}\{{{id}}}\Connection");
            if (connection?.GetValue("PnPInstanceId") is not string pnp ||
                !pnp.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                pnp.Length <= prefix.Length || pnp[prefix.Length] is not ('&' or '\\')) continue;
            if (CM_Locate_DevNodeW(out uint node, pnp, 0) != 0) continue;
            if (CM_Get_DevNode_Status(out uint status, out uint problem, node, 0) != 0) continue;
            var properties = adapter.GetIPProperties();
            int index = properties.GetIPv4Properties()?.Index ?? 0;
            if (index == 0) continue;
            var address = properties.UnicastAddresses.FirstOrDefault(a =>
                a.DuplicateAddressDetectionState == DuplicateAddressDetectionState.Preferred &&
                a.PrefixLength == 24 && Pool.Contains(a.Address.ToString()) &&
                addresses.Count(candidate => candidate == a.Address.ToString()) == 1);
            result.Add(new UsbAdapter(id, $"USB · {index}", index, address?.Address, (status & 0x400) != 0 ? problem : 0));
        }
        return result.OrderBy(a => a.Index).ToArray();
    }
    public static async Task PrepareAsync(string id)
    {
        if (!Guid.TryParse(id, out Guid guid)) throw new IOException("Invalid USB identity.");
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = true, Verb = "runas" };
        info.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(AppContext.BaseDirectory, "Prepare-MultiUsb.ps1") + "\" -AdapterId " + guid.ToString("D");
        using var process = Process.Start(info) ?? throw new IOException("USB setup did not start.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new IOException("USB setup was not completed. Check the adapter and try again.");
    }
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Locate_DevNodeW(out uint node, string deviceId, uint flags);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint node, uint flags);
}
