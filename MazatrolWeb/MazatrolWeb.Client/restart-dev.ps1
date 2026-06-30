# Stop stray dev servers and rebuild MazatrolWeb.Client
$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectDir

Write-Host "Stopping dotnet dev servers for this project..."
Get-CimInstance Win32_Process -Filter "name='dotnet.exe'" |
    Where-Object { $_.CommandLine -match 'MazatrolWeb\.Client' } |
    ForEach-Object {
        Write-Host "  Killing PID $($_.ProcessId)"
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

Start-Sleep -Seconds 1

Write-Host "Cleaning obj/bin..."
if (Test-Path obj) { Remove-Item -Recurse -Force obj }
if (Test-Path bin) { Remove-Item -Recurse -Force bin }

Write-Host "Building..."
dotnet build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Starting..."
dotnet run
