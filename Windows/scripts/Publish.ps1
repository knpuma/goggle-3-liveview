param([ValidateSet('win-x64','win-arm64')][string]$Runtime='win-x64', [string]$Output)
$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (!$Output) { $Output = Join-Path $project "outputs/Goggle-3-LiveView-Windows-$($Runtime -replace '^win-', '')" }
$architecture = $Runtime -replace '^win-', ''
$decoder = Join-Path $project "Windows/third_party/ffmpeg-$architecture/ffmpeg.exe"
if (!(Test-Path $decoder)) { throw 'Decoder missing for this architecture. See Windows/README.md.' }
dotnet test (Join-Path $project 'Windows/Goggles3.Core.Tests/Goggles3.Core.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
dotnet publish (Join-Path $project 'Windows/Goggles3.Windows/Goggles3.Windows.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
New-Item -ItemType Directory -Force (Join-Path $Output 'ffmpeg') | Out-Null
Copy-Item $decoder (Join-Path $Output 'ffmpeg/ffmpeg.exe')
Copy-Item (Join-Path (Split-Path $decoder -Parent) 'COPYING.LGPLv2.1') (Join-Path $Output 'ffmpeg/')
Copy-Item (Join-Path $project 'LICENSE') $Output
Copy-Item (Join-Path $project 'THIRD_PARTY_NOTICES.md') $Output
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
New-Item -ItemType Directory -Force (Join-Path $Output 'Licenses') | Out-Null
$runtimePack = Get-ChildItem (Join-Path $nuget "microsoft.netcore.app.runtime.$Runtime") -Directory | Sort-Object Name -Descending | Select-Object -First 1
$desktopPack = Get-ChildItem (Join-Path $nuget "microsoft.windowsdesktop.app.runtime.$Runtime") -Directory | Sort-Object Name -Descending | Select-Object -First 1
Copy-Item (Join-Path $runtimePack.FullName 'LICENSE.TXT') (Join-Path $Output 'Licenses/DotNET-LICENSE.txt')
Copy-Item (Join-Path $runtimePack.FullName 'THIRD-PARTY-NOTICES.TXT') (Join-Path $Output 'Licenses/DotNET-THIRD-PARTY-NOTICES.txt')
Copy-Item (Join-Path $desktopPack.FullName 'LICENSE') (Join-Path $Output 'Licenses/WPF-LICENSE.txt')
Copy-Item (Join-Path $project 'Windows/README.md') (Join-Path $Output 'README.md')
Write-Host "Package created: $Output"
