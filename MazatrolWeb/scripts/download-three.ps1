# Download Three.js r168 for offline use
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$target = Join-Path $base "..\MazatrolWeb.Client\wwwroot\lib\three"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Invoke-WebRequest -Uri "https://unpkg.com/three@0.168.0/build/three.module.js" -OutFile (Join-Path $target "three.module.js")
Invoke-WebRequest -Uri "https://unpkg.com/three@0.168.0/examples/jsm/controls/OrbitControls.js" -OutFile (Join-Path $target "OrbitControls.js")
Write-Host "Three.js downloaded to $target"
