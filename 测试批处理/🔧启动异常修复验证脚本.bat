@echo off
chcp 65001 >nul
echo 🔧 启动异常修复验证脚本
echo =====================================
echo.

echo 📋 本次修复的问题：
echo    • CompleteContractState方法运行时异常
echo    • 状态补全逻辑导致程序停止
echo    • null引用和LINQ操作异常
echo.

echo 🔍 修复验证步骤：
echo.

echo 步骤1: 检查修复的关键代码
findstr /n "CompleteContractState.*state.*config" "Services\AutoMonitorScanExecutor.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ CompleteContractState方法已修复
) else (
    echo ❌ CompleteContractState方法未找到
)

findstr /n "try.*catch.*COMPLETE-STATE-ERROR" "Services\AutoMonitorScanExecutor.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 异常处理逻辑已添加
) else (
    echo ❌ 异常处理逻辑缺失
)

findstr /n "state.*==.*null.*config.*==.*null" "Services\AutoMonitorScanExecutor.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ Null检查已添加
) else (
    echo ❌ Null检查缺失
)
echo.

echo 步骤2: 启动测试建议
echo =====================================
echo.
echo 🚀 测试步骤：
echo 1. 启动应用程序
echo 2. 打开自动盯盘配置窗口
echo 3. 点击"查看监控"按钮
echo 4. 观察是否出现异常停止
echo.

echo 📊 期望的日志输出：
echo ✅ 应该看到：
echo    🔧 [COMPLETE-STATE] 开始配置补全: BTCUSDT_LONG
echo    ✅ [COMPLETE-STATE] 配置补全完成: BTCUSDT_LONG -^> 配置: 狂飙
echo    🔄 [UPDATE-STATE] 更新现有状态: BTCUSDT_LONG, 浮盈: XXX.XXU
echo.
echo ❌ 不应该看到：
echo    ❌ [COMPLETE-STATE-ERROR] 配置补全失败
echo    ❌ [SCAN-ERROR] 处理合约 XXX 时发生异常
echo    程序异常停止或线程退出
echo.

echo 🔍 故障排除指南：
echo =====================================
echo.
echo 如果仍然出现异常，请检查：
echo.
echo 1. 📁 状态文件内容检查：
echo    文件位置: %%APPDATA%%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json
echo    检查文件结构是否完整，特别是：
echo    - breakEvenConfig 是否存在
echo    - addPositionConfig.tiers 是否为数组
echo    - profitProtectionConfig.tiers 是否为数组
echo.

echo 2. 🔧 配置文件检查：
echo    文件位置: %%APPDATA%%\BinanceFuturesTrader\AutoMonitorConfigs.json
echo    检查配置"狂飙"是否包含完整的：
echo    - breakEvenConfig
echo    - addPositionConfig.tiers
echo    - profitProtectionConfig.tiers
echo.

echo 3. 📝 详细日志获取：
echo    在Visual Studio中启动调试模式
echo    查看完整的异常堆栈跟踪
echo    特别关注CompleteContractState方法的异常
echo.

echo 4. 🧪 测试环境验证：
echo    如果问题持续，可以尝试：
echo    - 删除状态文件，让程序重新创建
echo    - 切换到其他测试账户
echo    - 检查是否有其他合约状态干扰
echo.

echo 📋 关键修复点总结：
echo =====================================
echo ✅ 添加了try-catch异常处理，防止程序崩溃
echo ✅ 增加了null检查，避免null引用异常
echo ✅ 改进了判断逻辑，更安全的配置补全
echo ✅ 完整保持执行状态，不会重置已执行的操作
echo ✅ 增强了日志记录，便于问题诊断
echo.

echo 🎯 如果修复成功，您应该能够：
echo • 正常启动自动盯盘
echo • 查看监控面板而不出现异常
echo • 看到正确的合约状态显示
echo • 保持已执行的操作状态不变
echo.

echo 📞 如果问题仍然存在：
echo • 请提供完整的异常日志
echo • 检查状态文件的具体内容
echo • 确认配置文件的结构是否正确
echo.

echo =====================================
echo 🔧 启动异常修复验证完成！
echo.
pause 