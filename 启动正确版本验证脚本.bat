@echo off
echo ====================================
echo       程序版本验证启动脚本
echo ====================================
echo.

echo 正在检查编译版本...
echo.

if exist "bin\Debug\net6.0-windows\BinanceFuturesTrader.exe" (
    echo ✅ Debug版本存在: bin\Debug\net6.0-windows\BinanceFuturesTrader.exe
) else (
    echo ❌ Debug版本不存在
)

if exist "bin\Release\net6.0-windows\BinanceFuturesTrader.exe" (
    echo ✅ Release版本存在: bin\Release\net6.0-windows\BinanceFuturesTrader.exe
) else (
    echo ❌ Release版本不存在
)

echo.
echo 请选择启动版本:
echo [1] Debug版本 (推荐用于测试)
echo [2] Release版本 (推荐用于生产)
echo [3] 退出
echo.

set /p choice=请输入选择 (1-3): 

if "%choice%"=="1" (
    echo.
    echo 🚀 启动Debug版本...
    echo 路径: bin\Debug\net6.0-windows\BinanceFuturesTrader.exe
    echo.
    echo ⚠️ 启动后请检查日志中是否出现:
    echo    "🔧 === 开始超级详细调试 ==="
    echo.
    pause
    start "" "bin\Debug\net6.0-windows\BinanceFuturesTrader.exe"
) else if "%choice%"=="2" (
    echo.
    echo 🚀 启动Release版本...
    echo 路径: bin\Release\net6.0-windows\BinanceFuturesTrader.exe
    echo.
    echo ⚠️ 启动后请检查日志中是否出现:
    echo    "🔧 === 开始超级详细调试 ==="
    echo.
    pause
    start "" "bin\Release\net6.0-windows\BinanceFuturesTrader.exe"
) else if "%choice%"=="3" (
    exit
) else (
    echo 无效选择，请重新运行脚本
    pause
)

echo.
echo 程序已启动，请检查以下内容:
echo 1. 日志中是否有超详细调试信息
echo 2. 监控面板是否有"🧹 清理状态"按钮
echo 3. 如果有，请先使用清理功能
echo.
pause 