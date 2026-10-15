using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Goggles3.Windows;

internal static class AdapterLocator
{
    public static Task<IPAddress?> FindAsync() => Task.Run(Find);

    private static IPAddress? Find()
    {
        // Identify USB hardware, not a user-editable adapter name or any NIC
        // with the same subnet. Read the PnP identity directly to avoid starting
        // PowerShell/WMI on every connection (very slow on a fresh Windows VM).
        const string networkKey = @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}";
        const string prefix = @"USB\VID_2CA3&PID_0020";
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            string id = adapter.Id.Trim('{','}');
            using var connection = Registry.LocalMachine.OpenSubKey($@"{networkKey}\{{{id}}}\Connection");
            if (connection?.GetValue("PnPInstanceId") is not string pnp ||
                !pnp.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                pnp.Length <= prefix.Length || pnp[prefix.Length] is not ('&' or '\\')) continue;
            if (CM_Locate_DevNodeW(out uint node, pnp, 0) != 0) continue;
            if (CM_Get_DevNode_Status(out uint status, out uint problem, node, 0) != 0)
                throw new IOException("Could not check the goggles USB status.");
            if ((status & 0x400) != 0) // DN_HAS_PROBLEM
                throw new IOException($"Windows cannot activate the goggles USB device (code {problem}). Reconnect the cable; in a virtual machine, check USB forwarding.");
            foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                if (address.Address.Equals(IPAddress.Parse("192.168.60.1")) && address.PrefixLength == 24) return address.Address;
        }
        return null;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Locate_DevNodeW(out uint node, string deviceId, uint flags);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint node, uint flags);

    public static async Task PrepareAsync()
    {
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = true, Verb = "runas" };
        info.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(AppContext.BaseDirectory, "Prepare-Usb.ps1") + "\"";
        using var process = Process.Start(info) ?? throw new IOException("USB setup did not start.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new IOException("Could not set up the goggles adapter. See the tutorial.");
    }
}
