@echo off
setlocal EnableDelayedExpansion

rem ============================================================
rem  AW Dollar - starter script
rem  Builds the app (only needed the first time / after changes)
rem  and launches it. Just double-click this file on Windows.
rem ============================================================

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\AW_Dollar\AW_Dollar.csproj"
set "EXE=%ROOT%src\AW_Dollar\bin\Release\net8.0-windows\AW_Dollar.exe"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [AW Dollar] .NET SDK was not found.
    echo Please install the .NET 8 SDK from the link below, then run this file again:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

if not exist "%PROJECT%" (
    echo [AW Dollar] Project file not found: %PROJECT%
    echo This script must stay next to the "src" folder.
    pause
    exit /b 1
)

echo [AW Dollar] Building the app...
dotnet build "%PROJECT%" -c Release -v quiet
if errorlevel 1 (
    echo.
    echo [AW Dollar] Build failed. See the output above for details.
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo [AW Dollar] Executable not found: %EXE%
    pause
    exit /b 1
)

echo [AW Dollar] Starting...
start "" "%EXE%"

endlocal
exit /b 0
