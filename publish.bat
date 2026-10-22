@echo off
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
    echo [AW Dollar] .NET SDK was not found.
    echo Please install the .NET 8 SDK from the link below, then run this file again:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo [AW Dollar] Building a self-contained release...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if errorlevel 1 (
    echo.
    echo [AW Dollar] Build failed. See the output above for details.
    pause
    exit /b 1
)

echo.
echo [AW Dollar] Done. Output:
echo %ROOT%src\AW_Dollar\bin\Release\net8.0-windows\win-x64\publish\AW_Dollar.exe
echo.
pause
exit /b 0
