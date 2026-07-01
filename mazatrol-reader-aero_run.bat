@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM ---------------------------------------------------------------------------
REM  Mazatrol Reader Aero — build and run (Blazor WebAssembly)
REM  Double-click or:  run.bat
REM  Opens the app (usually http://localhost:5101)
REM ---------------------------------------------------------------------------

cd /d "%~dp0"
set "REPO_ROOT=%CD%"
set "CLIENT_PROJ=%REPO_ROOT%\MazatrolWeb\MazatrolWeb.Client\MazatrolWeb.Client.csproj"
set "CONFIG=Debug"
set "EXIT_CODE=0"

echo.
echo === Mazatrol Reader Aero ===
echo Repo: %REPO_ROOT%
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found on PATH.
    echo Install .NET 10 SDK: https://dotnet.microsoft.com/download
    set "EXIT_CODE=1"
    goto :done
)

for /f "delims=" %%V in ('dotnet --version 2^>nul') do set "DOTNET_VER=%%V"
echo .NET SDK: !DOTNET_VER!
echo.

echo [1/2] Building client project ^(%CONFIG%^)...
dotnet build "%CLIENT_PROJ%" -c %CONFIG% --verbosity minimal
if errorlevel 1 (
    echo ERROR: dotnet build failed.
    set "EXIT_CODE=1"
    goto :done
)

echo.
echo [2/2] Starting Blazor WebAssembly app...
echo       Press Ctrl+C to stop.
echo.

dotnet run --project "%CLIENT_PROJ%" -c %CONFIG% --no-build --launch-profile http
set "EXIT_CODE=!ERRORLEVEL!"

:done
echo.
if not "%EXIT_CODE%"=="0" (
    echo Finished with errors ^(exit %EXIT_CODE%^).
    echo.
    pause
) else (
    echo Stopped.
    echo.
    pause
)
endlocal & exit /b %EXIT_CODE%