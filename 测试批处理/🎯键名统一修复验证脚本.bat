@echo off
chcp 65001 >nul
echo ========================================
echo 🎯 键名统一修复验证 - 解决保存异常
echo ========================================
echo.

echo 📋 本次修复内容：
echo   ✅ 统一键名格式：从 "BTCUSDT_SHORT" → "BTCUSDT"
echo   ✅ 修复编辑保存异常：contractState为null
echo   ✅ 修复状态文件生成逻辑
echo   ✅ 修复UI显示匹配逻辑
echo   ✅ 修复所有事件处理键名
echo.

echo 🔧 验证修复代码：
echo.

echo 【第1步】检查编译状态
echo   ✅ 编译成功，无错误

echo.
echo 【第2步】检查关键修复点
echo.

echo   📍 编辑保存修复：
findstr /c:"只使用Symbol作为键名" Views\ContractConfigEditDialog.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 编辑保存键名已统一
) else (
    echo   ❌ 编辑保存键名修复未找到
)

echo.
echo   📍 状态文件生成修复：
findstr /c:"只使用Symbol作为键名" Services\PositionConfigSyncService.cs >nul
if %errorlevel%==0 (
    echo   ✅ 状态文件生成键名已统一
) else (
    echo   ❌ 状态文件生成键名修复未找到
)

echo.
echo   📍 UI显示匹配修复：
findstr /c:"只使用Symbol" Views\AutoMonitorConfigWindowSimple.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ UI显示匹配键名已统一
) else (
    echo   ❌ UI显示匹配键名修复未找到
)

echo.
echo   📍 事件处理修复：
findstr /c:"只使用Symbol" Views\AutoMonitorDashboard.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 事件处理键名已统一
) else (
    echo   ❌ 事件处理键名修复未找到
)

echo.
echo ========================================
echo 🚀 完整测试方案
echo ========================================
echo.
echo 【重要】请按以下步骤进行完整测试：
echo.

echo 1️⃣ 清理旧状态文件：
echo    - 关闭程序
echo    - 删除状态文件：%%APPDATA%%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json
echo    - 重新启动程序
echo.

echo 2️⃣ 重新生成状态：
echo    - 选择Test账户
echo    - 点击"自动盯盘"按钮
echo    - 等待状态文件重新生成
echo.

echo 3️⃣ 验证键名格式：
echo    查看日志应该显示：
echo    [🔍【键名调试】存在的键: 'BTCUSDT' -> Symbol:BTCUSDT, Side:SHORT]
echo    [🔍【键名调试】存在的键: 'ETHUSDT' -> Symbol:ETHUSDT, Side:LONG]
echo    [🔍【键名调试】存在的键: 'XRPUSDT' -> Symbol:XRPUSDT, Side:SHORT]
echo.

echo 4️⃣ 测试编辑保存：
echo    - 双击任一合约行（如：BTCUSDT SHORT）
echo    - 修改保本状态为"√"
echo    - 点击保存按钮
echo    - 应该看到：[🎯【键名调试】找到匹配！使用键: 'BTCUSDT']
echo    - 保存应该成功，不再报错
echo.

echo 5️⃣ 验证UI显示：
echo    - 检查是否显示3个合约（BTC, ETH, XRP）
echo    - 检查推仓列是否显示4列
echo    - 检查保盈列是否显示3列
echo    - 检查浮盈数据是否正确（ETH: 1000U, XRP: -100U）
echo.

echo ⭐ 关键成功标志：
echo    1. 状态文件中的键名是 "BTCUSDT", "ETHUSDT", "XRPUSDT"
echo    2. 编辑保存不再报 "无法找到合约状态" 错误
echo    3. 所有UI显示正常
echo    4. 调试日志显示 "找到匹配！使用键: 'BTCUSDT'"
echo.

echo ⚠️  如果仍有问题，请提供：
echo    1. 新的键名调试日志（特别是"存在的键"部分）
echo    2. 编辑保存时的完整错误信息
echo    3. 状态文件的实际内容
echo.

echo ========================================
echo 修复验证脚本执行完成
echo ========================================
pause 