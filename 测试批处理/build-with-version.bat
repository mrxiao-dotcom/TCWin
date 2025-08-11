@echo off
REM ===================================================================
REM 编译并版本管理 - build-with-version.bat
REM 功能：编译项目，成功后自动升级版本号
REM ===================================================================

chcp 65001 >nul
echo.
echo 🔨 币安期货交易管理器 - 构建与版本管理
echo ═══════════════════════════════════════════════════════════════════

set "config=Release"
set "versionType=%1"
set "updateMessage=%2"

REM 设置默认版本类型
if "%versionType%"=="" set "versionType=patch"

echo 📋 构建配置：%config%
echo 📦 版本类型：%versionType%
if not "%updateMessage%"=="" echo 📝 更新说明：%updateMessage%

echo.
echo 🔄 开始编译项目...
echo ───────────────────────────────────────────────────────────────────

REM 编译项目
dotnet build BinanceFuturesTrader.csproj --configuration %config% --verbosity minimal

REM 检查编译结果
if %ERRORLEVEL% neq 0 (
    echo.
    echo ❌ 编译失败！版本不会升级。
    echo 💡 请修复编译错误后重试。
    pause
    exit /b 1
)

echo.
echo ✅ 编译成功！
echo ───────────────────────────────────────────────────────────────────

REM 询问是否升级版本
echo.
set /p upgrade="🚀 编译成功！是否升级版本号？ [Y/n]: "

if /i "%upgrade%"=="n" (
    echo 📌 版本号保持不变
    goto :end
)

echo.
echo 🔄 正在升级版本...

REM 执行版本升级
if "%updateMessage%"=="" (
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType "%versionType%" -Interactive
) else (
    powershell -ExecutionPolicy Bypass -File "AutoVersion.ps1" -VersionType "%versionType%" -UpdateMessage "%updateMessage%"
)

if %ERRORLEVEL% neq 0 (
    echo ❌ 版本升级失败！
    pause
    exit /b 1
)

echo.
echo 🎉 构建和版本管理完成！

:end
echo.
echo 📁 输出文件位置：
if exist "bin\%config%\net6.0-windows\BinanceFuturesTrader.exe" (
    for %%F in ("bin\%config%\net6.0-windows\BinanceFuturesTrader.exe") do (
        echo    路径：%%~fF
        echo    大小：%%~zF 字节
        echo    时间：%%~tF
    )
    echo.
    echo 💡 可以运行以下命令启动程序：
    echo    cd /d "bin\%config%\net6.0-windows" ^&^& BinanceFuturesTrader.exe
)

echo.
echo ✅ 所有操作完成！
pause 