param([Parameter(Mandatory=$true)][Guid]$AdapterId, [switch]$Restore)
$ErrorActionPreference = 'Stop'
$mutex = New-Object System.Threading.Mutex($false, 'Global\Goggle3MultiUsbSetup')
$locked = $false
try {
    $locked = $mutex.WaitOne(30000)
    if (!$locked) { throw 'Another USB configuration is still running.' }
    $devices = @(Get-CimInstance Win32_NetworkAdapter | Where-Object {
        $_.PNPDeviceID -match '^USB\\VID_2CA3&PID_0020(?:&|\\)' -and
        $_.GUID -and ([Guid]$_.GUID) -eq $AdapterId
    })
    if ($devices.Count -ne 1) { throw 'The selected DJI Goggles 3 USB adapter is not present.' }
    $device = $devices[0]
    if ($device.ConfigManagerErrorCode -ne 0) { throw "Windows USB device error: $($device.ConfigManagerErrorCode)." }
    $adapter = @(Get-NetAdapter -IncludeHidden | Where-Object { $_.InterfaceGuid -eq $AdapterId })
    if ($adapter.Count -ne 1) { throw 'The USB RNDIS adapter is not available.' }
    $index = $adapter[0].ifIndex
    $pool = @('192.168.60.1','192.168.60.3','192.168.60.4','192.168.60.5')
    $backupDir = Join-Path $env:ProgramData 'Goggle3LiveViewMulti'
    $backupPath = Join-Path $backupDir ($AdapterId.ToString('D') + '.json')
    if ($Restore) {
        if (!(Test-Path $backupPath)) { throw 'No saved USB configuration exists.' }
        $old = Get-Content $backupPath -Raw | ConvertFrom-Json
        if ([Guid]$old.Guid -ne $AdapterId) { throw 'Backup identity mismatch.' }
        foreach ($ip in @($old.Added)) {
            if ($ip -notin $pool) { throw 'Invalid address in the USB backup.' }
            Get-NetIPAddress -InterfaceIndex $index -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Where-Object IPAddress -eq $ip | Remove-NetIPAddress -Confirm:$false
        }
        foreach ($ip in @($old.Removed)) {
            if ($ip -notin $pool) { throw 'Invalid address in the USB backup.' }
            if (!(Get-NetIPAddress -InterfaceIndex $index -IPAddress $ip -ErrorAction SilentlyContinue)) {
                New-NetIPAddress -InterfaceIndex $index -IPAddress $ip -PrefixLength 24 | Out-Null
            }
        }
        if ($old.Dhcp -eq 'Enabled') { Set-NetIPInterface -InterfaceIndex $index -AddressFamily IPv4 -Dhcp Enabled }
        Remove-Item -LiteralPath $backupPath
        exit 0
    }
    $addresses = @(Get-NetIPAddress -InterfaceIndex $index -AddressFamily IPv4 -ErrorAction SilentlyContinue)
    $other = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object InterfaceIndex -ne $index)
    $usable = @($addresses | Where-Object { $_.IPAddress -in $pool -and $_.PrefixLength -eq 24 -and $_.IPAddress -notin $other.IPAddress })
    if ($usable.Count -gt 0) { exit 0 }
    if (@($addresses | Where-Object { $_.IPAddress -notin $pool -and $_.IPAddress -notlike '169.254.*' }).Count -gt 0) {
        throw 'This adapter has a custom IPv4 address. Its configuration has not been changed.'
    }
    $chosen = $pool | Where-Object { $_ -notin $other.IPAddress } | Select-Object -First 1
    if (!$chosen) { throw 'All four USB addresses are occupied.' }
    New-Item -ItemType Directory -Force $backupDir | Out-Null
    if (Test-Path $backupPath) { $old = Get-Content $backupPath -Raw | ConvertFrom-Json }
    else {
        $old = [pscustomobject]@{Guid=$AdapterId.ToString('D'); Dhcp=(Get-NetIPInterface -InterfaceIndex $index -AddressFamily IPv4).Dhcp.ToString(); Added=@(); Removed=@()}
    }
    $remove = @($addresses | Where-Object { $_.IPAddress -in $pool })
    $old.Removed = @(@($old.Removed) + @($remove.IPAddress | Where-Object { $_ -notin $old.Added }) | Select-Object -Unique)
    $old.Added = @(@($old.Added) + @($chosen) | Select-Object -Unique)
    $old | ConvertTo-Json | Set-Content -Encoding UTF8 $backupPath
    $remove | Remove-NetIPAddress -Confirm:$false
    New-NetIPAddress -InterfaceIndex $index -IPAddress $chosen -PrefixLength 24 -AddressFamily IPv4 | Out-Null
    # No DNS, default gateway, firewall, driver or non-DJI adapter changes.
    exit 0
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
} finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
