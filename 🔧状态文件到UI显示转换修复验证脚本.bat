@echo off
chcp 65001 > nul
echo 🔧 状态文件到UI显示转换修复验证脚本
echo.

echo 📋 问题描述：
echo   文件中显示 executionState: 2（已执行），但UI上仍然显示为"未触发"
echo   根本原因：PopulateConfigFromState方法只检查IsExecuted属性，忽略了ExecutionState
echo.

echo 🔍 修复内容：
echo.

echo 【修复1：保本状态转换逻辑】
echo   原来：config.BreakEvenStatus = state.BreakEvenConfig.IsExecuted ? "√" : "-";
echo   修复后：根据ExecutionState进行转换
echo     - NotTriggered(0) → "-" (未触发)
echo     - Executing(1) → "⚡" (执行中)
echo     - Executed(2) → "√" (已执行)
echo.

echo 【修复2：推仓状态转换逻辑】
echo   原来：var status = tier.IsExecuted ? "√" : "-";
echo   修复后：根据ExecutionState进行转换
echo     - NotTriggered(0) → "-" (未触发)
echo     - Executing(1) → "⚡" (执行中)
echo     - Executed(2) → "√" (已执行)
echo.

echo 【修复3：保盈状态转换逻辑】
echo   原来：var status = tier.IsExecuted ? "√" : "-";
echo   修复后：根据ExecutionState进行转换
echo     - NotTriggered(0) → "-" (未触发)
echo     - Executing(1) → "⚡" (执行中)
echo     - Executed(2) → "√" (已执行)
echo.

echo 【修复4：详细调试日志】
echo   新增状态加载调试信息，显示：
echo     - 保本状态: 显示符号 (ExecutionState: 状态值)
echo     - 推仓阶梯状态: 显示符号 (ExecutionState: 状态值)
echo     - 保盈阶梯状态: 显示符号 (ExecutionState: 状态值)
echo.

echo 🎯 验证步骤：
echo.

echo 【步骤1：编译最新代码】
echo   1. 关闭程序
echo   2. 重新编译: dotnet build TCWin.sln --configuration Release
echo   3. 启动程序
echo.

echo 【步骤2：验证现有状态文件】
echo   1. 检查 contract_monitoring_states.json 文件内容
echo   2. 确认文件中有 executionState: 2 的配置
echo   3. 打开"启动盯盘面板"
echo   4. 检查UI是否正确显示"√"（已执行）
echo.

echo 【步骤3：验证执行中状态】
echo   1. 手动设置某个状态为"执行中"
echo   2. 关闭界面，重新打开
echo   3. 检查UI是否显示"⚡"（执行中）
echo   4. 检查日志中的调试信息
echo.

echo 【步骤4：验证调试日志】
echo   1. 打开"启动盯盘面板"
echo   2. 检查日志输出，应该看到：
echo      ✅ 已加载合约配置: BTCUSDT_LONG
echo         📊 保本状态: √ (ExecutionState: Executed)
echo         📊 推仓阶梯1状态: √ (ExecutionState: Executed)
echo         📊 推仓阶梯2状态: - (ExecutionState: NotTriggered)
echo         📊 保盈阶梯1状态: ⚡ (ExecutionState: Executing)
echo.

echo 📊 预期结果：
echo.

echo 【场景1：executionState: 0 (NotTriggered)】
echo   文件中: "executionState": 0
echo   UI显示: "-" (未触发)
echo   日志: 📊 保本状态: - (ExecutionState: NotTriggered)
echo.

echo 【场景2：executionState: 1 (Executing)】
echo   文件中: "executionState": 1
echo   UI显示: "⚡" (执行中)
echo   日志: 📊 推仓阶梯1状态: ⚡ (ExecutionState: Executing)
echo.

echo 【场景3：executionState: 2 (Executed)】
echo   文件中: "executionState": 2
echo   UI显示: "√" (已执行)
echo   日志: 📊 保本状态: √ (ExecutionState: Executed)
echo.

echo 🔍 问题诊断：
echo.

echo 【如果UI仍然显示错误】
echo   1. 检查日志中的ExecutionState值是否正确
echo   2. 确认状态文件中的executionState字段值
echo   3. 检查是否有缓存问题，尝试重启程序
echo   4. 验证PopulateConfigFromState方法是否被正确调用
echo.

echo 【如果调试信息不显示】
echo   1. 确认LoadContractConfigsFromStateFile方法被调用
echo   2. 检查日志输出窗口是否正常工作
echo   3. 验证状态文件是否被正确读取
echo.

echo 🎉 修复验证要点：
echo   ✅ executionState: 2 的配置应显示为"√"
echo   ✅ executionState: 1 的配置应显示为"⚡"
echo   ✅ executionState: 0 的配置应显示为"-"
echo   ✅ 调试日志应显示详细的状态转换信息
echo   ✅ 所有状态符号应与文件中的数值一致
echo.

echo 💡 修复完成！
echo   现在状态文件中的执行状态能够正确显示在UI上了
echo. 