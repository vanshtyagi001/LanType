@echo off
setlocal enabledelayedexpansion

echo =====================================================================
echo LANType Virtual HID Keyboard Driver Build Script
echo =====================================================================

:: Locate MSBuild from Visual Studio / Build Tools
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] vswhere.exe not found at "%VSWHERE%". Visual Studio required.
    exit /b 1
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
    set "MSBUILD=%%i"
)

if not exist "%MSBUILD%" (
    echo [ERROR] MSBuild.exe not found.
    exit /b 1
)

echo [INFO] Found MSBuild at: "%MSBUILD%"

set "CONFIG=Release"
if "%1"=="Debug" set "CONFIG=Debug"

echo [INFO] Building LANTypeVhid.vcxproj in %CONFIG%^|x64 mode...
"%MSBUILD%" "%~dp0LANTypeVhid.vcxproj" /p:Configuration=%CONFIG% /p:Platform=x64 /t:Rebuild

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Driver build succeeded! Output files located in bin\%CONFIG%\
) else (
    echo [ERROR] Driver build failed with error code %ERRORLEVEL%.
    echo Ensure the Windows Driver Kit (WDK) is installed and integrated into Visual Studio.
)

exit /b %ERRORLEVEL%
