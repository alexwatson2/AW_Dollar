@echo off
chcp 65001 >nul
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
    echo [AW Dollar] .NET SDK پیدا نشد.
    echo .NET 8 SDK را از آدرس زیر نصب کنید و دوباره این فایل را اجرا کنید:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

if not exist "%PROJECT%" (
    echo [AW Dollar] فایل پروژه پیدا نشد: %PROJECT%
    echo این فایل باید کنار پوشه src اجرا شود.
    pause
    exit /b 1
)

echo [AW Dollar] در حال ساخت برنامه...
dotnet build "%PROJECT%" -c Release -v quiet
if errorlevel 1 (
    echo.
    echo [AW Dollar] ساخت برنامه با خطا مواجه شد. پیام بالا را بررسی کنید.
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo [AW Dollar] فایل اجرایی پیدا نشد: %EXE%
    pause
    exit /b 1
)

echo [AW Dollar] در حال اجرا...
start "" "%EXE%"

endlocal
exit /b 0
