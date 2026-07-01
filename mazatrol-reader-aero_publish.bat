@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM ---------------------------------------------------------------------------
REM  Mazatrol Reader Aero — Publish Static Files
REM  Creates a ready-to-host folder in MazatrolWeb.Client\publish
REM ---------------------------------------------------------------------------

cd /d "%~dp0"
set "REPO_ROOT=%CD%"
set "CLIENT_PROJ=%REPO_ROOT%\MazatrolWeb\MazatrolWeb.Client\MazatrolWeb.Client.csproj"
set "PUBLISH_DIR=%REPO_ROOT%\MazatrolWeb\MazatrolWeb.Client\publish"

echo.
echo === Mazatrol Reader Aero - Publish ===
echo.

echo Publishing Release build...
dotnet publish "%CLIENT_PROJ%" -c Release -o "%PUBLISH_DIR%" --no-self-contained

if errorlevel 1 (
    echo.
    echo ERROR: Publish failed.
    pause
    exit /b 1
)

echo.
echo Publish completed successfully!
echo Output folder: %PUBLISH_DIR%\wwwroot
echo.

echo You can now serve the app with:
echo   dotnet serve --directory "%PUBLISH_DIR%\wwwroot"
echo.
echo Or use any static file server (nginx, IIS, Vercel, etc.)
echo.

pause