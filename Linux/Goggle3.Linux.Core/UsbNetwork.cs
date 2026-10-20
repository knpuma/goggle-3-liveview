using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace Goggles3.Core;

public sealed record UsbAdapter(string Name, int Index, bool Configured);

public static partial class UsbNetwork
{
    public static readonly IPAddress LocalAddress = IPAddress.Parse("192.168.60.1");
    [GeneratedRegex("^[a-zA-Z0-9_.-]{1,15}$")]
    private static partial Regex SafeName();

    // Resolve the actual sysfs USB ancestry. Names, descriptions and IP addresses
    // alone are not sufficient evidence that an adapter belongs to DJI.
    public static bool IsGoggles(string name, string sysRoot = "/sys")
    {
        if (!SafeName().IsMatch(name) || name is "." or "..") return false;
        try
        {
            var link = new DirectoryInfo(Path.Combine(sysRoot, "class/net", name, "device"));
            DirectoryInfo? node = link.ResolveLinkTarget(true) as DirectoryInfo;
            while (node != null && node.FullName.StartsWith(Path.GetFullPath(sysRoot) + "/", StringComparison.Ordinal))
            {
                string vendor = Path.Combine(node.FullName, "idVendor");
                string product = Path.Combine(node.FullName, "idProduct");
                if (File.Exists(vendor) && File.Exists(product))
                    return File.ReadAllText(vendor).Trim().Equals("2ca3", StringComparison.OrdinalIgnoreCase)
                        && File.ReadAllText(product).Trim().Equals("0020", StringComparison.OrdinalIgnoreCase);
                node = node.Parent;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return false;
    }

    public static IReadOnlyList<UsbAdapter> Find()
    {
        if (!OperatingSystem.IsLinux()) return [];
        var result = new List<UsbAdapter>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!IsGoggles(nic.Name)) continue;
            var properties = nic.GetIPProperties();
            int index = properties.GetIPv4Properties()?.Index ?? 0;
            bool configured = properties.UnicastAddresses.Any(a => a.Address.Equals(LocalAddress) && a.PrefixLength == 24)
                && nic.OperationalStatus == OperationalStatus.Up;
            if (index > 0) result.Add(new(nic.Name, index, configured));
        }
        return result;
    }

    public static void CheckConflict(UsbAdapter selected)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.Name == selected.Name) continue;
            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                byte[] bytes = address.Address.GetAddressBytes();
                if (bytes.Length == 4 && bytes[0] == 192 && bytes[1] == 168 && bytes[2] == 60)
                    throw new IOException("Another adapter uses 192.168.60.x. Disconnect that network before connecting the goggles.");
            }
        }
    }

    public static async Task PrepareAsync(UsbAdapter adapter)
    {
        CheckConflict(adapter);
        if (!IsGoggles(adapter.Name)) throw new IOException("The DJI USB adapter was disconnected.");
        if (!adapter.Configured)
        {
            const string helper = "/usr/lib/goggle-3-liveview/prepare-usb.py";
            if (!File.Exists(helper)) throw new IOException("Install the .deb package, or configure USB using the Linux setup guide.");
            var info = new ProcessStartInfo("/usr/bin/pkexec") { UseShellExecute = false,
                RedirectStandardError = true, RedirectStandardOutput = true };
            info.ArgumentList.Add("/usr/bin/python3"); info.ArgumentList.Add("-I");
            info.ArgumentList.Add(helper); info.ArgumentList.Add(adapter.Name); info.ArgumentList.Add(adapter.Index.ToString());
            using var process = Process.Start(info) ?? throw new IOException("Could not open the system authorization dialog.");
            var errors = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            await output;
            if (process.ExitCode != 0) throw new IOException("USB configuration was not authorized or failed. " + await errors);
        }
        if (!Find().Any(a => a.Name == adapter.Name && a.Index == adapter.Index && a.Configured))
            throw new IOException("USB is not ready. Reconnect the cable and check the Linux setup guide.");
    }
}
