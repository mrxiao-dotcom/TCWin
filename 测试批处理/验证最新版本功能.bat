@echo off
chcp 65001 >nul
echo.
echo ========================================
echo       验证最新版本功能检查
echo ========================================
echo.

echo 📅 检查时间: %date% %time%
echo.

echo 🔍 步骤1: 检查程序文件时间戳...
if exist "bin\Release\net6.0-windows\BinanceFuturesTrader.dll" (
    for %%f in ("bin\Release\net6.0-windows\BinanceFuturesTrader.dll") do (
        set /a sizekb=%%~zf/1024
        echo ✅ 主程序库文件存在
        echo 📊 文件大小: %%~zf 字节 (!sizekb! KB)
        echo 📅 修改时间: %%~tf
        
        if !sizekb! GEQ 700 (
            echo ✅ 文件大小正常 (应该约800KB)
        ) else (
            echo ❌ 文件大小异常！可能编译不完整
        )
    )
) else (
    echo ❌ 主程序库文件不存在！请先编译程序
    pause
    exit /b 1
)
echo.

echo 🔍 步骤2: 检查源代码中的新功能...

echo 📋 检查监控面板清理按钮功能...
findstr /C:"🧹 清理状态" "Views\AutoMonitorDashboard.xaml" >nul
if %errorlevel% == 0 (
    echo ✅ 发现监控面板清理状态按钮代码
) else (
    echo ❌ 未找到监控面板清理状态按钮代码
)

findstr /C:"ClearStatesButton_Click" "Views\AutoMonitorDashboard.xaml.cs" >nul
if %errorlevel% == 0 (
    echo ✅ 发现清理状态按钮点击事件处理代码
) else (
    echo ❌ 未找到清理状态按钮点击事件处理代码
)

echo.
echo 📋 检查超详细调试日志功能...
findstr /C:"🔧 === 开始超级详细调试 ===" "Services\AutoMonitorService.cs" >nul
if %errorlevel% == 0 (
    echo ✅ 发现超详细调试日志代码
) else (
    echo ❌ 未找到超详细调试日志代码
)

findstr /C:"阶梯.*完整状态" "Services\AutoMonitorService.cs" >nul
if %errorlevel% == 0 (
    echo ✅ 发现阶梯详细状态检查代码
) else (
    echo ❌ 未找到阶梯详细状态检查代码
)

echo.
echo 🔍 步骤3: 启动程序进行功能验证...
echo.
echo ⚠️  重要提醒：
echo    1. 关闭当前正在运行的旧版本程序
echo    2. 启动新编译的程序: bin\Release\net6.0-windows\BinanceFuturesTrader.exe
echo    3. 检查以下功能是否存在：
echo.
echo 📋 监控面板功能验证清单：
echo    [ ] 启动程序后，点击"自动盯盘监控面板"
echo    [ ] 检查监控面板右上角是否有"🧹 清理状态"按钮
echo    [ ] 点击"🧹 清理状态"按钮，确认弹出确认对话框
echo.
echo 📋 调试日志功能验证清单：
echo    [ ] 配置并启动自动盯盘功能
echo    [ ] 观察控制台或日志文件中是否出现：
echo        "🔧 === 开始超级详细调试 ==="
echo    [ ] 检查是否有详细的阶梯状态信息：
echo        "🔧 阶梯X完整状态: 启用=True, 触发金额=X.XU..."
echo.
echo 🎯 如果所有检查都通过，说明新版本功能正常！
echo.
echo ========================================
echo 🚀 快速启动新版本程序：
echo ========================================
echo.
choice /C YN /M "是否现在启动新编译的程序进行测试？(Y/N)"
if %errorlevel% == 1 (
    echo.
    echo 🚀 正在启动新版本程序...
    start "" "bin\Release\net6.0-windows\BinanceFuturesTrader.exe"
    echo.
    echo ✅ 程序已启动！请按照上面的验证清单进行功能检查。
    echo.
    echo 💡 验证提示：
    echo    - 如果看不到🧹清理状态按钮，说明仍在运行旧版本
    echo    - 如果日志中没有🔧调试信息，请检查自动盯盘是否正确启动
    echo.
) else (
    echo.
    echo ℹ️  手动启动程序路径: bin\Release\net6.0-windows\BinanceFuturesTrader.exe
)

echo.
echo ========================================
echo 验证完成！如果发现问题请联系技术支持。
echo ========================================
pause 