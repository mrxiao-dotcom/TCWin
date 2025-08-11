@echo off
REM ===================================================================
REM VS2022快捷版本升级(Patch) - vs-version-patch.bat
REM 功能：从VS2022快速启动Patch版本升级
REM ===================================================================

chcp 65001 >nul
cd /d "%~dp0"

echo.
echo 📦 Visual Studio 2022 - 版本升级 (Patch)
echo ═══════════════════════════════════════════════════════════════════
echo.

REM 检查AutoVersion.ps1是否存在
if not exist "AutoVersion.ps1" (
    echo ❌ 找不到 AutoVersion.ps1 文件
    echo 💡 请确保此文件在项目根目录下
    pause
    exit /b 1
)

echo 🚀 启动Patch版本升级...
powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType patch -Interactive

echo.
echo ✅ 版本升级完成！
echo 💡 可以返回VS2022继续开发
pause 