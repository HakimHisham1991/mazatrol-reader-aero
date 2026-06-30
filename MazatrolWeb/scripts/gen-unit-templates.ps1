$unitsDir = Join-Path $PSScriptRoot "..\MazatrolWeb.Client\wwwroot\units"
New-Item -ItemType Directory -Force -Path $unitsDir | Out-Null

$lin = New-Object byte[] 100
$lin[0] = 168
$lin[8] = 1
[IO.File]::WriteAllBytes((Join-Path $unitsDir "LIN.unit"), $lin)

$tpr = New-Object byte[] 100
$tpr[0] = 168
$tpr[8] = 2
[IO.File]::WriteAllBytes((Join-Path $unitsDir "TPR.unit"), $tpr)

$facing = New-Object byte[] 400
$facing[0] = 51
[IO.File]::WriteAllBytes((Join-Path $unitsDir "FACING.unit"), $facing)

Get-ChildItem $unitsDir | Format-Table Name, Length
