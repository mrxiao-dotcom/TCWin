@echo off
chcp 65001 >nul
echo.
echo ========================================
echo         启动最新版本程序
echo ========================================
echo.

echo 📅 当前时间: %date% %time%
echo.

echo 🔍 步骤1: 检查程序文件...
if exist "bin\Release\net6.0-windows\BinanceFuturesTrader.exe" (
    for %%f in ("bin\Release\net6.0-windows\BinanceFuturesTrader.exe") do (
        echo ✅ 主程序文件: %%~nxf
        echo 📅 编译时间: %%~tf
        echo 📊 文件大小: %%~zf 字节
    )
    
    for %%f in ("bin\Release\net6.0-windows\BinanceFuturesTrader.dll") do (
        set /a sizekb=%%~zf/1024
        echo ✅ 核心库文件: %%~nxf  
        echo 📅 编译时间: %%~tf
        echo 📊 文件大小: %%~zf 字节 (!sizekb! KB)
        
        if !sizekb! GEQ 700 (
            echo ✅ 文件大小正常，包含新功能
        ) else (
            echo ❌ 文件大小异常！
        )
    )
) else (
    echo ❌ 程序文件不存在！
    pause
    exit /b 1
)

echo.
echo 🔍 步骤2: 检查新功能代码...
findstr /C:"ClearStatesButton" "Views\AutoMonitorDashboard.xaml" >nul
if %errorlevel% == 0 (
    echo ✅ 清理状态按钮代码已确认存在
) else (
    echo ❌ 清理状态按钮代码缺失
)

echo.
echo 🚀 步骤3: 启动最新版本程序...
echo.
echo ⚠️  重要说明：
echo    启动后请检查监控面板右上角是否有3个按钮：
echo    [ ] 🧹 清理状态
echo    [ ] 🔄 回刷  
echo    [ ] ❌ 关闭
echo.
echo    如果只有2个按钮，说明启动的仍是旧版本！
echo.

echo 🚀 正在启动最新版本程序...
echo 📍 程序路径: %cd%\bin\Release\net6.0-windows\BinanceFuturesTrader.exe
echo.

start "" "%cd%\bin\Release\net6.0-windows\BinanceFuturesTrader.exe"

echo ✅ 程序已启动！
echo.
echo 📋 验证步骤：
echo 1. 等待程序完全加载
echo 2. 点击"自动盯盘监控面板"按钮
echo 3. 检查右上角是否有"🧹 清理状态"按钮
echo 4. 如果没有，请重复此流程
echo.
echo ========================================
echo 如果仍然看不到新功能，请联系技术支持
echo ========================================
pause 