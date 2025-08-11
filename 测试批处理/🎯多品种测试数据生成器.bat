@echo off
chcp 65001 >nul
echo 🎯 多品种测试数据生成器
echo =====================================
echo.

echo 📋 将生成3个测试合约：
echo    📊 BTCUSDT LONG: 浮盈 +250.75U (正常盈利)
echo    📊 ETHUSDT LONG: 浮盈 +1000.00U (大幅盈利)
echo    📊 XRPUSDT SHORT: 浮盈 -100.00U (亏损场景)
echo.

echo 🔧 步骤1: 清空现有状态文件
set "STATE_DIR=%APPDATA%\BinanceFuturesTrader\Accounts\Test"
set "STATE_FILE=%STATE_DIR%\contract_monitoring_states.json"

if not exist "%STATE_DIR%" (
    mkdir "%STATE_DIR%" 2>nul
)

if exist "%STATE_FILE%" (
    echo 📁 备份现有状态文件...
    copy "%STATE_FILE%" "%STATE_FILE%.backup.%date:~0,4%%date:~5,2%%date:~8,2%" >nul 2>&1
    del "%STATE_FILE%" >nul 2>&1
    echo ✅ 已清空状态文件
) else (
    echo ✅ 无现有状态文件
)
echo.

echo 🔧 步骤2: 生成多品种测试状态文件
echo 📝 创建包含3个品种的测试状态文件...

echo { > "%STATE_FILE%"
echo   "BTCUSDT_LONG": { >> "%STATE_FILE%"
echo     "Symbol": "BTCUSDT", >> "%STATE_FILE%"
echo     "PositionSide": "LONG", >> "%STATE_FILE%"
echo     "Quantity": 0.05, >> "%STATE_FILE%"
echo     "EntryPrice": 49500.00, >> "%STATE_FILE%"
echo     "MarkPrice": 50050.15, >> "%STATE_FILE%"
echo     "UnrealizedPnl": 250.75, >> "%STATE_FILE%"
echo     "IsActive": true, >> "%STATE_FILE%"
echo     "IsEnabled": true, >> "%STATE_FILE%"
echo     "LastUpdateTime": "2024-01-01T12:00:00Z", >> "%STATE_FILE%"
echo     "BaseConfigName": "测试配置", >> "%STATE_FILE%"
echo     "Name": "测试配置_BTCUSDT" >> "%STATE_FILE%"
echo   }, >> "%STATE_FILE%"
echo   "ETHUSDT_LONG": { >> "%STATE_FILE%"
echo     "Symbol": "ETHUSDT", >> "%STATE_FILE%"
echo     "PositionSide": "LONG", >> "%STATE_FILE%"
echo     "Quantity": 5.0, >> "%STATE_FILE%"
echo     "EntryPrice": 3000.00, >> "%STATE_FILE%"
echo     "MarkPrice": 3200.00, >> "%STATE_FILE%"
echo     "UnrealizedPnl": 1000.00, >> "%STATE_FILE%"
echo     "IsActive": true, >> "%STATE_FILE%"
echo     "IsEnabled": true, >> "%STATE_FILE%"
echo     "LastUpdateTime": "2024-01-01T12:00:00Z", >> "%STATE_FILE%"
echo     "BaseConfigName": "测试配置", >> "%STATE_FILE%"
echo     "Name": "测试配置_ETHUSDT" >> "%STATE_FILE%"
echo   }, >> "%STATE_FILE%"
echo   "XRPUSDT_SHORT": { >> "%STATE_FILE%"
echo     "Symbol": "XRPUSDT", >> "%STATE_FILE%"
echo     "PositionSide": "SHORT", >> "%STATE_FILE%"
echo     "Quantity": 2000.0, >> "%STATE_FILE%"
echo     "EntryPrice": 0.67, >> "%STATE_FILE%"
echo     "MarkPrice": 0.62, >> "%STATE_FILE%"
echo     "UnrealizedPnl": -100.00, >> "%STATE_FILE%"
echo     "IsActive": true, >> "%STATE_FILE%"
echo     "IsEnabled": true, >> "%STATE_FILE%"
echo     "LastUpdateTime": "2024-01-01T12:00:00Z", >> "%STATE_FILE%"
echo     "BaseConfigName": "测试配置", >> "%STATE_FILE%"
echo     "Name": "测试配置_XRPUSDT" >> "%STATE_FILE%"
echo   } >> "%STATE_FILE%"
echo } >> "%STATE_FILE%"

if exist "%STATE_FILE%" (
    echo ✅ 多品种测试状态文件生成成功
    echo 📁 文件位置: %STATE_FILE%
    
    echo.
    echo 📊 文件大小: 
    for %%A in ("%STATE_FILE%") do echo    %%~zA 字节
    
    echo.
    echo 📋 文件内容验证:
    findstr /c:"BTCUSDT" "%STATE_FILE%" >nul && echo    ✅ 包含 BTCUSDT
    findstr /c:"ETHUSDT" "%STATE_FILE%" >nul && echo    ✅ 包含 ETHUSDT  
    findstr /c:"XRPUSDT" "%STATE_FILE%" >nul && echo    ✅ 包含 XRPUSDT
    findstr /c:"1000.00" "%STATE_FILE%" >nul && echo    ✅ ETH浮盈1000设置正确
    findstr /c:"-100.00" "%STATE_FILE%" >nul && echo    ✅ XRP浮盈-100设置正确
) else (
    echo ❌ 状态文件生成失败
)
echo.

echo 🚀 步骤3: 启动程序验证
echo.
echo 📝 验证要点：
echo    • 启动程序选择Test账户
echo    • 打开自动盯盘配置窗口
echo    • 应该看到3个测试合约
echo    • 检查浮盈数值和颜色显示
echo.

echo 💡 预期结果：
echo    📋 BTCUSDT LONG: +250.75U (绿色)
echo    📋 ETHUSDT LONG: +1000.00U (绿色) 
echo    📋 XRPUSDT SHORT: -100.00U (红色)
echo.

echo 🎯 多品种测试数据生成完成！
echo 按任意键启动程序进行验证...
pause >nul

if exist "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe" (
    start "" "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe"
    echo ✅ 程序已启动，请验证3个测试合约的显示效果
) else (
    echo ❌ 未找到编译后的程序文件，请先编译项目
)

echo.
echo 🎯 验证完成！如需重新生成测试数据，请再次运行此脚本。
pause 