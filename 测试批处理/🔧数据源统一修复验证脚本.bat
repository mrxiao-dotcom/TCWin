@echo off
chcp 65001 > nul

REM 这个脚本现在用于生成多品种测试数据
echo 🎯 多品种测试数据生成器
echo =====================================
echo.

echo 📋 将生成以下测试数据：
echo    • BTCUSDT_LONG: 浮盈 +250.75U (保本已执行, 推仓1已执行)
echo    • ETHUSDT_LONG: 浮盈 +500.00U (保本已执行, 推仓1&2已执行, 保盈1已执行)
echo    • XRPUSDT_SHORT: 浮盈 -100.00U (全部未触发)
echo.

echo 🎯 覆盖的测试场景：
echo    ✅ 正常盈利场景 (BTC)
echo    ✅ 大幅盈利场景 (ETH) 
echo    ✅ 亏损场景 (XRP)
echo    ✅ 不同执行状态组合
echo    ✅ 多阶段推仓测试
echo    ✅ 保盈机制测试
echo.

set /p confirm="是否继续生成测试数据? (y/n): "
if /i "%confirm%" neq "y" (
    echo 操作已取消
    pause
    exit /b
)

echo.
echo 🔧 开始生成测试数据...

REM 确定状态文件路径
set "STATE_DIR=%APPDATA%\BinanceFuturesTrader\Accounts\Test"
set "STATE_FILE=%STATE_DIR%\contract_monitoring_states.json"

REM 创建目录
if not exist "%STATE_DIR%" (
    mkdir "%STATE_DIR%"
    echo ✅ 创建目录: %STATE_DIR%
)

REM 备份现有文件
if exist "%STATE_FILE%" (
    for /f "tokens=1-3 delims=/ " %%a in ('date /t') do set "datestr=%%c%%a%%b"
    for /f "tokens=1-2 delims=: " %%a in ('time /t') do set "timestr=%%a%%b"
    set "BACKUP_FILE=%STATE_FILE%.backup_%datestr%_%timestr%"
    copy "%STATE_FILE%" "!BACKUP_FILE!" >nul
    echo 📋 已备份现有状态文件
)
echo   2. 检查：是否自动生成contract_monitoring_states.json文件
echo   3. 检查：UI数据是否与文件内容完全一致
echo   4. 预期：文件生成后UI立即显示对应数据
echo.
echo 【测试3：数据源唯一性验证】
echo   1. 手动删除状态文件
echo   2. 重新打开面板
echo   3. 检查：UI是否变为空白（无数据显示）
echo   4. 预期：不应从其他路径获取数据
echo.

echo 请按任意键启动程序进行测试...
pause > nul

start "" "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe"

echo.
echo 🔍 程序已启动，请按照上述步骤进行验证：
echo.
echo 📁 关键文件位置：
echo   状态文件: %%APPDATA%%\BinanceFuturesTrader\Accounts\{账户名}\contract_monitoring_states.json
echo   基础配置: %%APPDATA%%\BinanceFuturesTrader\Global\auto_monitor_configs.json
echo.
echo 🚨 修复前可能存在的后门路径（现已移除）：
echo   ❌ CreateExampleContractData() - 示例数据创建
echo   ❌ CreateContractMonitorFromProfile() - 从旧PositionProfile创建
echo   ❌ _autoMonitorService.GetPositionProfiles() - 旧档案系统
echo.
echo ✅ 修复后的统一数据流程：
echo   📄 contract_monitoring_states.json → ConvertStateToContractMonitor → UI显示
echo.

echo 验证完成后请按任意键查看结果总结...
pause > nul

echo.
echo 📊 验证结果检查清单：
echo.
echo □ 测试1：没有状态文件时UI为空
echo □ 测试2：有持仓时自动生成状态文件
echo □ 测试3：删除状态文件后UI变空
echo □ 文件内容与UI显示完全一致
echo □ 触发金额正确显示（95而不是0）
echo.
echo 如发现任何问题，请检查日志输出！
echo.
echo �� 数据源统一修复验证完成！
pause 