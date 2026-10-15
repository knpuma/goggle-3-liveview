param([switch]$Restore)
$ErrorActionPreference = 'Stop'
try {
    # Scope is fixed in code: never touch Wi-Fi/Ethernet or a NIC selected by name.
    $devices = @(Get-CimInstance Win32_NetworkAdapter | Where-Object {
        $_.PNPDeviceID -match '^USB\\VID_2CA3&PID_0020(?:&|\\)'
    })
    if ($devices.Count -ne 1) { throw 'Exactly one DJI Goggles 3 RNDIS adapter is required (VID 2CA3, PID 0020).' }
    $device = $devices[0]
    if (!$Restore -and $device.ConfigManagerErrorCode -ne 0) {
        throw "Windows cannot activate the goggles USB device (code $($device.ConfigManagerErrorCode)). Reconnect the cable or check virtual machine USB forwarding."
    }
    $adapter = Get-NetAdapter -IncludeHidden | Where-Object { $_.InterfaceGuid.ToString().Trim('{}') -eq $device.GUID.Trim('{}') }
    if (@($adapter).Count -ne 1) { throw 'The RNDIS driver has not created a usable adapter.' }
    $index = $adapter.ifIndex
    $backupDir = Join-Path $env:ProgramData 'Goggles3Mac'
    $backupPath = Join-Path $backupDir ($device.GUID.Trim('{}') + '.json')
    if ($Restore) {
        if (!(Test-Path $backupPath)) { throw 'No backup exists for the previous goggles adapter configuration.' }
        $previous = Get-Content $backupPath -Raw | ConvertFrom-Json
        if ($previous.Added) {
            Get-NetIPAddress -InterfaceIndex $index -AddressFamily IPv4 -ErrorAction SilentlyContinue |
              Where-Object { $_.IPAddress -eq '192.168.60.1' } | Remove-NetIPAddress -Confirm:$false
        }
        if ($previous.Dhcp -eq 'Enabled') { Set-NetIPInterface -InterfaceIndex $index -AddressFamily IPv4 -Dhcp Enabled }
        exit 0
    }
    $addresses = @(Get-NetIPAddress -InterfaceIndex $index -AddressFamily IPv4 -ErrorAction SilentlyContinue)
    if (@($addresses | Where-Object { $_.IPAddress -eq '192.168.60.1' -and $_.PrefixLength -eq 24 }).Count -gt 0) { exit 0 }
    if (@($addresses | Where-Object { $_.IPAddress -ne '192.168.60.1' -and $_.IPAddress -notlike '169.254.*' }).Count -gt 0) {
        throw 'The goggles adapter already has a different static address. It has not been changed; review it manually.'
    }
    $conflict = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -eq '192.168.60.1' -and $_.InterfaceIndex -ne $index }
    if ($conflict) { throw '192.168.60.1 already belongs to another adapter. No network settings have been changed.' }
    New-Item -ItemType Directory -Force $backupDir | Out-Null
    if (!(Test-Path $backupPath)) {
        [pscustomobject]@{ Guid=$device.GUID; Dhcp=(Get-NetIPInterface -InterfaceIndex $index -AddressFamily IPv4).Dhcp.ToString(); Added=$true } |
          ConvertTo-Json | Set-Content -Encoding UTF8 $backupPath
    }
    New-NetIPAddress -InterfaceIndex $index -IPAddress '192.168.60.1' -PrefixLength 24 -AddressFamily IPv4 | Out-Null
    # No default gateway, DNS, firewall change, driver installation or adapter reset.
    exit 0
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
