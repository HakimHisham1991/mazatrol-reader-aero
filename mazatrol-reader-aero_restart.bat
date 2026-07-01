@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM ---------------------------------------------------------------------------
REM  Mazatrol Reader Aero — Restart Dev Server
REM  Cleans build artifacts and restarts the Blazor WASM app
REM ---------------------------------------------------------------------------

cd /d "%~dp0"
set "REPO_ROOT=%CD%"
set "CLIENT_PROJ=%REPO_ROOT%\MazatrolWeb\MazatrolWeb.Client\MazatrolWeb.Client.csproj"
set "CONFIG=Debug"

echo.
echo === Mazatrol Reader Aero - Restart Dev ===
echo.

echo Stopping any running dotnet processes...
taskkill /f /im dotnet.exe >nul 2>&1

echo.
echo Cleaning previous build artifacts...
if exist "%REPO_ROOT%\MazatrolWeb\MazatrolWeb.Client\bin" rmdir /s /q "%REPO_ROOT%\MazatrolWeb\MazatrolWeb.Client\bin"
if exist "%REPO_ROOT%\MazatrolWeb\MazatrolWeb.Client\obj" rmdir /s /q "%REPO_ROOT%\MazatrolWeb\MazatrolWeb.Client\obj"

echo.
echo Restoring packages...
dotnet restore "%CLIENT_PROJ%"

echo.
echo Building...
dotnet build "%CLIENT_PROJ%" -c %CONFIG% --verbosity minimal

echo.
echo Starting development server...
echo.
dotnet run --project "%CLIENT_PROJ%" -c %CONFIG% --launch-profile http

echo.
echo If the server doesn't start, run run.bat instead.
pause