@echo off
chcp 65001 >nul
echo ========================================
echo 🎯 数据源干扰问题最终测试验证
echo ========================================
echo.

echo 📋 本次修复的关键问题：
echo   ✅ 键名统一：BTCUSDT_SHORT → BTCUSDT
echo   ✅ 保存异常：contractState为null → 已修复
echo   ✅ 数据源干扰：RefreshPositionDataAsync覆盖状态文件 → 已消除
echo   ✅ UI列生成：推仓4列、保盈3列强制显示
echo   ✅ 文件驱动：状态文件成为唯一数据源
echo.

echo 🔧 验证修复是否成功：
echo.

echo 【第1步】检查关键修复代码
findstr /c:"只从统一状态文件重新加载" Views\AutoMonitorConfigWindowSimple.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ 数据源干扰修复已应用
) else (
    echo   ❌ 数据源干扰修复未找到
)

findstr /c:"不调用RefreshPositionDataAsync" Views\AutoMonitorConfigWindowSimple.xaml.cs >nul
if %errorlevel%==0 (
    echo   ✅ RefreshPositionDataAsync调用已移除
) else (
    echo   ❌ RefreshPositionDataAsync调用仍存在
)

echo.
echo ========================================
echo 🚀 完整功能测试方案
echo ========================================
echo.

echo 【重要提醒】测试前请执行以下步骤：
echo.
echo 1️⃣ 清理旧状态文件（键名格式已改变）：
echo    - 关闭程序
echo    - 删除文件：%%APPDATA%%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json
echo    - 重新启动程序
echo.

echo 2️⃣ 基础功能验证：
echo    - 选择Test账户
echo    - 点击"自动盯盘"按钮
echo    - 等待加载完成
echo.
echo    预期结果：
echo    ✅ 显示3个合约（BTCUSDT SHORT, ETHUSDT LONG, XRPUSDT SHORT）
echo    ✅ 推仓列显示4列（推仓1-4档）
echo    ✅ 保盈列显示3列（保盈1-3档）
echo    ✅ 浮盈显示：BTC: 0U, ETH: 1000U, XRP: -100U
echo.

echo 3️⃣ 关键测试：编辑保存功能
echo    - 双击BTCUSDT SHORT行
echo    - 修改保本状态为"√"
echo    - 点击保存按钮
echo.
echo    预期成功日志：
echo    [🎯【键名调试】找到匹配！使用键: 'BTCUSDT']
echo    [🔄 配置已保存，从统一状态文件重新加载确保界面同步...]
echo    [✅ 合约配置保存完成，已从统一状态文件重新加载: BTCUSDT SHORT]
echo.
echo    预期UI变化：
echo    ✅ 保本状态变为"√"
echo    ✅ 其他数据保持不变（浮盈、推仓、保盈状态等）
echo    ✅ 合约数量仍为3个
echo    ✅ UI布局保持正常
echo.

echo 4️⃣ 数据一致性验证：
echo    - 保存后检查状态文件内容
echo    - 确认只有保本状态改变
echo    - 其他配置保持原有状态
echo.

echo ========================================
echo ❌ 如果仍有问题，可能的原因
echo ========================================
echo.
echo 问题1：保存仍报"无法找到合约状态"
echo   原因：旧状态文件使用BTCUSDT_SHORT格式，新代码使用BTCUSDT格式
echo   解决：必须删除旧状态文件让系统重新生成
echo.
echo 问题2：保存后显示了不同的数据
echo   原因：可能还有其他数据源干扰路径
echo   解决：检查日志中是否有ProfileService调用
echo.
echo 问题3：UI列数不正确
echo   原因：基础配置中缺少完整的阶梯配置
echo   解决：检查基础配置是否有3个保盈阶梯、4个推仓阶梯
echo.
echo 问题4：浮盈数据不正确
echo   原因：持仓匹配逻辑问题
echo   解决：检查mainViewModel.Positions是否包含测试数据
echo.

echo ========================================
echo 🎯 成功标准
echo ========================================
echo.
echo 【必须全部满足】：
echo 1. ✅ 编辑保存不再报错
echo 2. ✅ 保存后只有修改的字段变化
echo 3. ✅ 其他数据保持原状不被覆盖
echo 4. ✅ 日志显示"找到匹配！使用键: 'BTCUSDT'"
echo 5. ✅ 日志显示"从统一状态文件重新加载"
echo 6. ✅ 没有ProfileService相关的日志
echo 7. ✅ 推仓4列、保盈3列正常显示
echo 8. ✅ 浮盈数据正确（ETH: 1000U, XRP: -100U）
echo.

echo ⚠️  如果任何一项不满足，请提供：
echo    1. 完整的编辑保存日志
echo    2. UI界面截图（保存前后对比）
echo    3. 状态文件内容（保存前后对比）
echo    4. 具体哪一步出现了问题
echo.

echo ========================================
echo 数据源干扰最终测试完成
echo ========================================
pause 