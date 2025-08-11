@echo off
chcp 65001 >nul
echo.
echo ================================================================
echo 🔧 触发金额保护修复验证脚本
echo ================================================================
echo.
echo 📋 问题描述：
echo   编辑合约统一状态时（如修改推仓1状态从"已执行"改为"未触发"）
echo   会导致所有触发金额变为0，并写入文件
echo.
echo 🎯 修复内容：
echo   1. 保本触发金额保护：只在值>0且有变化时才更新
echo   2. 加载逻辑修复：确保BreakEvenTarget从状态文件正确加载
echo   3. 推仓触发金额保护：已有保护机制，只更新状态不更新金额
echo   4. 保盈触发金额保护：已有保护机制，只更新状态不更新金额
echo.
echo ================================================================
echo 🧪 测试步骤
echo ================================================================
echo.
echo **步骤1：检查修复前的状态文件**
echo   1. 打开自动盯盘面板
echo   2. 查看contract_monitoring_states.json文件
echo   3. 记录当前的触发金额（应该>0）
echo.
echo **步骤2：测试状态编辑**
echo   1. 双击任意合约行打开编辑窗口
echo   2. 修改推仓1状态：从"已执行"改为"未触发"（或相反）
echo   3. 点击"保存"按钮
echo   4. 关闭编辑窗口
echo.
echo **步骤3：验证触发金额保护**
echo   1. 重新检查contract_monitoring_states.json文件
echo   2. 验证触发金额是否保持原值（不应该变为0）
echo   3. 验证状态变化是否正确保存
echo.
echo **步骤4：查看详细日志**
echo   查找以下关键日志条目：
echo   - "🔒 保本触发金额保护: 保持原值"
echo   - "📋 加载保本触发金额"
echo   - "🔥【金额保护】推仓阶梯X只更新状态，保持原金额"
echo.
echo ================================================================
echo ✅ 预期结果
echo ================================================================
echo.
echo **修复前的问题**：
echo   触发金额被错误地设置为0：
echo   ```json
echo   "triggerProfitAmount": 0.0
echo   ```
echo.
echo **修复后的正确行为**：
echo   触发金额保持原值：
echo   ```json
echo   "triggerProfitAmount": 95.0  (保持原值)
echo   ```
echo.
echo **日志输出示例**：
echo   🔒 保本触发金额保护: 保持原值 95，跳过更新（编辑值: 0）
echo   📋 加载保本触发金额: 95
echo   🔥【金额保护】推仓阶梯1只更新状态，保持原金额: 300
echo.
echo ================================================================
echo 🚨 注意事项
echo ================================================================
echo.
echo 1. **重启程序**: 使用修复后的版本测试
echo 2. **备份数据**: 测试前备份contract_monitoring_states.json
echo 3. **多次测试**: 测试不同状态变化组合
echo 4. **查看日志**: 确认保护机制生效的日志输出
echo.
echo ================================================================
echo 📝 问题根源说明
echo ================================================================
echo.
echo **问题发生位置**：Views/ContractConfigEditDialog.xaml.cs 第592行
echo   contractState.BreakEvenConfig.TriggerProfitAmount = _editedConfig.BreakEvenTarget;
echo.
echo **问题原因**：
echo   1. _editedConfig.BreakEvenTarget 默认值为0
echo   2. 加载时没有从状态文件读取触发金额
echo   3. 保存时无条件覆盖触发金额
echo.
echo **修复方案**：
echo   1. 添加金额保护条件：只在值>0且有变化时更新
echo   2. 修复加载逻辑：从状态文件正确加载触发金额
echo   3. 添加详细日志：记录保护机制的工作状态
echo.
echo 按任意键继续...
pause >nul 