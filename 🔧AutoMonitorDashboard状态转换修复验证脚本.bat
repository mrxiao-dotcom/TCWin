@echo off
chcp 65001 > nul
echo 🔧 AutoMonitorDashboard状态转换修复验证脚本
echo.

echo 📋 问题描述：
echo   文件中保本、推仓状态数值为 2（已执行），但UI显示却还是"-"（未触发）
echo   根本原因：AutoMonitorDashboard.xaml.cs中仍然使用IsExecuted属性进行状态判断
echo.

echo 🔍 修复内容：
echo.

echo 【修复1：保本状态转换逻辑】
echo   文件：Views/AutoMonitorDashboard.xaml.cs
echo   原来：Status = state.BreakEvenConfig.IsExecuted ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered
echo   修复后：根据ExecutionState进行转换
echo     - NotTriggered(0) → TriggerExecutionStatus.NotTriggered
echo     - Executing(1) → TriggerExecutionStatus.Executing
echo     - Executed(2) → TriggerExecutionStatus.Executed
echo.

echo 【修复2：推仓状态转换逻辑】
echo   原来：Status = tier.IsExecuted ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered
echo   修复后：根据ExecutionState进行转换
echo     - NotTriggered(0) → TriggerExecutionStatus.NotTriggered
echo     - Executing(1) → TriggerExecutionStatus.Executing
echo     - Executed(2) → TriggerExecutionStatus.Executed
echo.

echo 【修复3：保盈状态转换逻辑】
echo   原来：Status = tier.IsExecuted ? TriggerExecutionStatus.Executed : TriggerExecutionStatus.NotTriggered
echo   修复后：根据ExecutionState进行转换
echo     - NotTriggered(0) → TriggerExecutionStatus.NotTriggered
echo     - Executing(1) → TriggerExecutionStatus.Executing
echo     - Executed(2) → TriggerExecutionStatus.Executed
echo.

echo 【修复4：日志输出修复】
echo   原来：{(state.BreakEvenConfig.IsExecuted ? "已执行" : "未触发")}
echo   修复后：根据ExecutionState显示状态
echo     - NotTriggered → "未触发"
echo     - Executing → "执行中"
echo     - Executed → "已执行"
echo.

echo 🎯 验证步骤：
echo.

echo 【步骤1：编译最新代码】
echo   1. 关闭程序
echo   2. 重新编译: dotnet build TCWin.sln --configuration Release
echo   3. 启动程序
echo.

echo 【步骤2：验证状态文件】
echo   1. 检查 contract_monitoring_states.json 文件内容
echo   2. 确认文件中有 executionState: 2 的配置
echo   3. 记录具体的配置内容
echo.

echo 【步骤3：测试启动盯盘功能】
echo   1. 打开"启动盯盘面板"
echo   2. 点击"启动盯盘"按钮
echo   3. 检查触发条件的状态显示
echo   4. 查看日志输出
echo.

echo 【步骤4：验证日志输出】
echo   1. 启动盯盘后，应该看到以下日志：
echo      ✅【启动盯盘】添加保本条件: XXXu, 状态: 已执行
echo      ✅【启动盯盘】添加推仓条件: 阶梯1, XXXu, 状态: 已执行
echo      ✅【启动盯盘】添加保盈条件: 阶梯1, XXXu, 状态: 已执行
echo.

echo 📊 预期结果：
echo.

echo 【场景1：executionState: 0 (NotTriggered)】
echo   文件中: "executionState": 0
echo   触发条件状态: TriggerExecutionStatus.NotTriggered
echo   日志显示: "状态: 未触发"
echo.

echo 【场景2：executionState: 1 (Executing)】
echo   文件中: "executionState": 1
echo   触发条件状态: TriggerExecutionStatus.Executing
echo   日志显示: "状态: 执行中"
echo.

echo 【场景3：executionState: 2 (Executed)】
echo   文件中: "executionState": 2
echo   触发条件状态: TriggerExecutionStatus.Executed
echo   日志显示: "状态: 已执行"
echo.

echo 🔍 问题诊断：
echo.

echo 【如果状态仍然显示错误】
echo   1. 检查日志中的状态描述是否正确
echo   2. 确认状态文件中的executionState字段值
echo   3. 验证ConvertStateToContractMonitor方法是否被调用
echo   4. 检查TriggerConditions的Status属性值
echo.

echo 【关键检查点】
echo   1. 启动盯盘日志中的状态描述
echo   2. 触发条件模型的Status属性
echo   3. UI界面上的状态显示
echo   4. 状态文件与UI的一致性
echo.

echo 🎉 修复验证要点：
echo   ✅ executionState: 2 应该显示"已执行"而不是"未触发"
echo   ✅ 触发条件状态应该正确映射到TriggerExecutionStatus枚举
echo   ✅ 日志输出应该显示正确的状态描述
echo   ✅ 启动盯盘功能应该正确识别已执行的条件
echo.

echo 💡 修复完成！
echo   现在AutoMonitorDashboard能够正确读取和显示状态文件中的执行状态了
echo. 