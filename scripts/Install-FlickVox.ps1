[CmdletBinding()]
param([switch]$SkipVoices)
$ErrorActionPreference='Stop'
$root=Join-Path $env:LOCALAPPDATA 'Programs\FlickVox'
$data=Join-Path $env:LOCALAPPDATA 'FlickVox'
$source=Join-Path $PSScriptRoot '..\src\FlickVox\bin\Release\net10.0-windows\win-x64\publish'
if (!(Test-Path $source)) { throw "Publish files not found. Run: dotnet publish src/FlickVox -c Release -r win-x64 --self-contained" }
New-Item -ItemType Directory -Force -Path $root,$data | Out-Null
Copy-Item "$source\*" $root -Recurse -Force
$start=Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'; $shortcut=Join-Path $start 'FlickVox.lnk'
$shell=New-Object -ComObject WScript.Shell; $link=$shell.CreateShortcut($shortcut);$link.TargetPath=Join-Path $root 'FlickVox.exe';$link.WorkingDirectory=$root;$link.Save()
Write-Host "Installed FlickVox to $root."
Write-Host "Install Piper in $data\runtime\piper and use Voice Manager to download voices."
