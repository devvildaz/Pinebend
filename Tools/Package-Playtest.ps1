$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$build=Join-Path $root 'Builds/Pinebend'
$zip=Join-Path $root 'Builds/Pinebend-Windows-Playtest.zip'
if (!(Test-Path (Join-Path $build 'Pinebend.exe'))) {throw 'Build the Windows player first.'}
Copy-Item (Join-Path $root 'Docs/Friend-Playtest.md') (Join-Path $build 'Friend-Playtest.md') -Force
$setup=Join-Path $build 'MultiplayerSetup'
New-Item -ItemType Directory -Path $setup -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'MultiplayerSetup') -File | Copy-Item -Destination $setup -Force
$files=Get-ChildItem -LiteralPath $build | Where-Object { $_.Name -notmatch 'BackUpThisFolder|BurstDebugInformation|DoNotShip' }
Compress-Archive -LiteralPath $files.FullName -DestinationPath $zip -CompressionLevel Fastest -Force
Get-Item $zip | Select-Object FullName,Length
