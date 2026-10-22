@echo off
chcp 65001 >nul
setlocal

rem ==================================================================
rem  AW Dollar - publish script
rem  Builds a single, self-contained AW_Dollar.exe that can be copied
rem  to any Windows PC and run without installing the .NET runtime.
rem ==================================================================

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\AW_Dollar\AW_Dollar.csproj"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [AW Dollar] .NET SDK پیدا نشد.
    echo .NET 8 SDK را از آدرس زیر نصب کنید و دوباره این فایل را اجرا کنید:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo [AW Dollar] در حال ساخت نسخه مستقل (Self-Contained)...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo.
    echo [AW Dollar] ساخت با خطا مواجه شد. پیام بالا را بررسی کنید.
    pause
    exit /b 1
)

echo.
echo [AW Dollar] ساخته شد:
echo %ROOT%src\AW_Dollar\bin\Release\net8.0-windows\win-x64\publish\AW_Dollar.exe
echo.
pause
exit /b 0
