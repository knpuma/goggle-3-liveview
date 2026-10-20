param([string]$Output)
$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (!$Output) { $Output = Join-Path $project 'outputs/Goggle-3-LiveView-Multi-Windows-x64-0.5.0' }
dotnet test (Join-Path $project 'Windows/Goggles3.Multi.Tests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
dotnet publish (Join-Path $project 'Windows/Goggles3.Multi.Windows') -c Release -r win-x64 --self-contained true -p:DebugType=None -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item (Join-Path $project 'Windows/third_party/ffmpeg-x64/ffmpeg.exe') $Output
Copy-Item (Join-Path $project 'LICENSE') $Output
Copy-Item (Join-Path $project 'Windows/Multi-README.md') (Join-Path $Output 'README.md')
Copy-Item (Join-Path $project 'Windows/Multi-LEEME.md') (Join-Path $Output 'LEEME.md')
Copy-Item (Join-Path $project 'Windows/Multi-NOTICES.md') (Join-Path $Output 'THIRD_PARTY_NOTICES.md')
$licenses = Join-Path $Output 'Licenses'
New-Item -ItemType Directory -Force $licenses | Out-Null
Copy-Item (Join-Path $project 'Windows/third_party/ffmpeg-x64/COPYING.LGPLv2.1') $licenses
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$runtime = Join-Path $nuget 'microsoft.netcore.app.runtime.win-x64/10.0.13'
$wpf = Join-Path $nuget 'microsoft.windowsdesktop.app.runtime.win-x64/10.0.13'
Copy-Item (Join-Path $runtime 'LICENSE.TXT') (Join-Path $licenses 'DotNET-LICENSE.txt')
Copy-Item (Join-Path $runtime 'THIRD-PARTY-NOTICES.TXT') (Join-Path $licenses 'DotNET-THIRD-PARTY-NOTICES.txt')
Copy-Item (Join-Path $wpf 'LICENSE') (Join-Path $licenses 'WPF-LICENSE.txt')
Compress-Archive -Path $Output -DestinationPath "$Output.zip" -Force
Get-FileHash "$Output.zip" -Algorithm SHA256
