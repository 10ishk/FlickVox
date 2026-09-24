[CmdletBinding()]
param([switch]$RemoveData)
$root=Join-Path $env:LOCALAPPDATA 'Programs\FlickVox'; $data=Join-Path $env:LOCALAPPDATA 'FlickVox'
if(Test-Path $root){Remove-Item -LiteralPath $root -Recurse -Force}
$shortcut=Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\FlickVox.lnk';if(Test-Path $shortcut){Remove-Item -LiteralPath $shortcut -Force}
if($RemoveData -and (Test-Path $data)){Remove-Item -LiteralPath $data -Recurse -Force}
Write-Host 'FlickVox removed. User data was preserved unless -RemoveData was supplied.'
