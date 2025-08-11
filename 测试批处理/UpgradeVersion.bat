@echo off
chcp 65001 > nul
echo ========================================
echo  Binance Futures Trader Version Tool
echo ========================================
echo.

if "%1"=="" (
    echo Usage: UpgradeVersion.bat [major^|minor^|patch^|preview^|help]
    echo.
    echo   major   - Upgrade major version (x.0.0)
    echo   minor   - Upgrade minor version (x.y.0)  
    echo   patch   - Upgrade patch version (x.y.z)
    echo   preview - Preview changes without applying
    echo   help    - Show detailed help
    echo.
    echo Example:
    echo   UpgradeVersion.bat patch
    echo   UpgradeVersion.bat preview
    echo.
    pause
    exit /b 0
)

if /i "%1"=="help" (
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -Help
    pause
    exit /b 0
)

if /i "%1"=="preview" (
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -Preview
    pause
    exit /b 0
)

if /i "%1"=="major" (
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType major
    pause
    exit /b 0
)

if /i "%1"=="minor" (
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType minor
    pause
    exit /b 0
)

if /i "%1"=="patch" (
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType patch
    pause
    exit /b 0
)

echo Error: Invalid parameter "%1"
echo Use "UpgradeVersion.bat help" for usage information.
pause 