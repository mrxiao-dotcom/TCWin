@echo off
chcp 65001 > nul
echo 🔧 状态编辑同步验证脚本
echo.

echo 📋 测试目标：
echo   验证双击合约配置，手工修改保本状态（未触发→已执行）
echo   是否正确同步保存到 contract_monitoring_states.json 文件
echo.

echo 🔍 当前的状态编辑同步流程：
echo.
echo ✅ 1. 双击编辑流程（Views/AutoMonitorConfigWindowSimple.xaml.cs）：
echo   - ContractConfigDataGrid_MouseDoubleClick 处理双击
echo   - 打开 ContractConfigEditDialog 编辑对话框
echo   - 传递当前配置给编辑对话框
echo.
echo ✅ 2. 编辑保存流程（Views/ContractConfigEditDialog.xaml.cs）：
echo   - 用户修改状态："-" (未触发) → "✓" (已执行)
echo   - 点击保存按钮触发 SaveButton_Click
echo   - 调用 SaveContractConfigToFile() 方法
echo   - 使用 ContractMonitoringStateService.UpdateExecutionStatus 保存到文件
echo   - 调用：stateService.UpdateExecutionStatus(contractKey, "BreakEven", null, true, 0, "手动设置为executed")
echo.
echo ✅ 3. UI刷新流程（返回主窗口）：
echo   - 调用 ReloadContractStateFromUnifiedFile(selectedConfig) 从文件重新加载状态
echo   - 调用 selectedConfig.NotifyAllPropertiesChanged() 刷新UI显示
echo   - 调用 RefreshPositionDataAsync() 刷新持仓数据
echo.

echo 🚀 详细验证步骤：
echo.
echo 【步骤1：准备环境】
echo   1. 确保已经有 contract_monitoring_states.json 文件
echo   2. 确保文件中保本状态为 "waiting" 或 ExecutionState: 0
echo   3. 确保UI中显示保本状态为 "-"
echo.
echo 【步骤2：执行编辑】
echo   1. 启动程序，打开"启动盯盘面板"
echo   2. 双击合约配置行（如 BTCUSDT LONG）
echo   3. 在编辑对话框中，将保本状态从"未触发"改为"已执行"
echo   4. 点击"保存"按钮
echo   5. 关闭编辑对话框
echo.
echo 【步骤3：验证结果】
echo   A. 检查日志输出：
echo      - 应该显示："🔧 开始更新合约状态: BTCUSDT_LONG"
echo      - 应该显示："✅ 保本状态更新为executed"
echo      - 应该显示："✅ 合约状态更新完成: BTCUSDT_LONG"
echo      - 应该显示："🔄 重新加载合约状态: BTCUSDT LONG"
echo      - 应该显示："📊 保本状态已更新: ✓"
echo.
echo   B. 检查UI显示：
echo      - 保本状态列应该显示"✓"而不是"-"
echo      - 状态变更应该立即生效
echo.
echo   C. 检查文件内容：
echo      - 打开 %%APPDATA%%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json
echo      - 找到对应合约的 BreakEvenConfig 部分
echo      - 验证 "executionState": 2 (对应 ExecutionState.Executed)
echo      - 验证 "isExecuted": true
echo.

echo 🎯 预期的完整日志输出：
echo.
echo [时间] 🖱️ 双击编辑合约配置: BTCUSDT LONG
echo [时间] 🔧 准备编辑配置，当前保本状态: -
echo [时间] 🔧 开始更新合约状态: BTCUSDT_LONG
echo [时间] 🔧 原始合约名: BTCUSDT LONG
echo [时间] 🔧 标准化合约键: BTCUSDT_LONG
echo [时间] 🔧 当前账号: Test
echo [时间] 🔧 待更新状态: 保本=✓, 推仓=T1:-|T2:-|T3:-|T4:-, 保盈=T1:-|T2:-|T3:-
echo [时间] 保存合约配置: BTCUSDT LONG
echo [时间] ✅ 保本状态更新为executed
echo [时间] ✅ 合约状态更新完成: BTCUSDT_LONG
echo [时间] 🔄 重新加载合约状态: BTCUSDT LONG
echo [时间] 🔍 尝试加载状态: 原始=BTCUSDT LONG, 标准化=BTCUSDT_LONG
echo [时间] 📊 保本状态已更新: ✓
echo [时间] ✅ 合约配置已更新: BTCUSDT LONG
echo.

echo 📄 预期的文件内容变化：
echo.
echo ✅ 修改前（contract_monitoring_states.json）：
echo   "BTCUSDT_LONG": {
echo     "breakEvenConfig": {
echo       "executionState": 0,
echo       "isExecuted": false,
echo       ...
echo     }
echo   }
echo.
echo ✅ 修改后（contract_monitoring_states.json）：
echo   "BTCUSDT_LONG": {
echo     "breakEvenConfig": {
echo       "executionState": 2,
echo       "isExecuted": true,
echo       "executionTime": "2024-01-01T12:00:00",
echo       "note": "手动设置为executed"
echo       ...
echo     }
echo   }
echo.

echo 🔧 关键技术实现：
echo.
echo ✅ 编辑对话框保存逻辑（ContractConfigEditDialog.SaveContractConfigToFile）：
echo   - 解析状态：BreakEvenStatus "✓" → isExecuted = true
echo   - 调用统一状态服务：stateService.UpdateExecutionStatus()
echo   - 保存到文件：contract_monitoring_states.json
echo.
echo ✅ 主窗口刷新逻辑（AutoMonitorConfigWindowSimple.ReloadContractStateFromUnifiedFile）：
echo   - 从文件重新加载：stateService.GetMonitoringState()
echo   - 更新UI模型：state.BreakEvenConfig.IsExecuted → config.BreakEvenStatus
echo   - 刷新显示：NotifyAllPropertiesChanged()
echo.

echo 🚨 注意事项：
echo.
echo 1. 合约键格式转换：
echo    - UI显示："BTCUSDT LONG" (带空格)
echo    - 文件键名："BTCUSDT_LONG" (下划线)
echo    - 代码使用 .Replace(" ", "_") 进行转换
echo.
echo 2. 状态符号映射：
echo    - UI符号："✓" = 已执行, "-" = 未触发
echo    - 文件状态：ExecutionState.Executed (2), ExecutionState.NotTriggered (0)
echo.
echo 3. 双向同步：
echo    - 编辑→文件：UI状态保存到JSON文件
echo    - 文件→UI：从JSON文件重新加载UI状态
echo.

echo 现在请按照上述步骤手动验证状态编辑同步功能！
echo 特别关注：编辑后文件内容是否真的更新了！
echo.
pause 