@echo off
chcp 65001 > nul
echo 🔧 状态更新调试脚本
echo.

echo 📋 问题描述：
echo   用户双击修改保本状态（未触发→已执行）后，保存应该让文件中的
echo   breakEvenConfig 部分发生变化，但实际上没有看到变化
echo.

echo 🔍 调试步骤和关键日志检查：
echo.

echo 【步骤1：检查文件更新前的状态】
echo   1. 导航到：%%APPDATA%%\BinanceFuturesTrader\Accounts\Test\
echo   2. 打开 contract_monitoring_states.json
echo   3. 找到对应合约的 breakEvenConfig 部分
echo   4. 记录当前的值：
echo      - "executionState": 应该是 0 (NotTriggered)
echo      - "isExecuted": 应该是 false
echo      - "executionTime": 应该是 null
echo.

echo 【步骤2：执行状态修改操作】
echo   1. 启动程序，打开"启动盯盘面板"
echo   2. 双击合约配置行
echo   3. 在编辑对话框中修改保本状态：未触发 → 已执行
echo   4. 点击保存按钮
echo   5. 关闭对话框
echo.

echo 【步骤3：检查关键日志输出】
echo   在日志窗口中寻找以下关键信息：
echo.
echo   A. 编辑对话框的保存日志：
echo      🔧 开始更新合约状态: BTCUSDT_LONG
echo      🔧 原始合约名: BTCUSDT LONG
echo      🔧 标准化合约键: BTCUSDT_LONG
echo      🔧 当前账号: Test
echo      🔧 待更新状态: 保本=✓, 推仓=...
echo.
echo   B. ContractMonitoringStateService 的关键日志：
echo      🔍【状态更新开始】BTCUSDT_LONG BreakEven_null = True
echo      📂 加载到 X 个状态
echo      ✅ 找到合约状态: BTCUSDT_LONG
echo      📊 更新前状态: BreakEven=False, AddPosition阶梯数=X, ProfitProtection阶梯数=X
echo      📊 更新后状态: BreakEven=True
echo      💾 开始保存到文件...
echo      ✅ 文件保存完成
echo      🔍 立即验证文件保存结果...
echo      ✅ 验证结果 - 保本状态: True
echo.
echo   C. ContractMonitoringStateGenerator 的详细日志：
echo      🔧【StateGenerator】更新执行状态: BTCUSDT_LONG BreakEven_null
echo      🎯 操作类型: BreakEven, 阶梯: null, 成功: True
echo      📊 保本状态更新: 更新前=False
echo      📊 保本状态更新: 更新后=True
echo.

echo 【步骤4：诊断可能的问题】
echo.
echo   🚨 如果看到日志中显示"✅ 保本状态更新为executed"但文件没变化：
echo.
echo   问题A：合约键不匹配
echo     - 检查日志中的"标准化合约键"是否与文件中的键一致
echo     - 文件中的键格式：BTCUSDT_LONG
echo     - UI显示格式：BTCUSDT LONG
echo     - 转换逻辑：.Replace(" ", "_")
echo.
echo   问题B：文件路径错误
echo     - 检查日志中是否有文件路径相关信息
echo     - 确认账号名是否正确（Test）
echo     - 确认目录是否存在
echo.
echo   问题C：权限或文件锁定问题
echo     - 检查文件是否被其他程序占用
echo     - 检查是否有写入权限
echo.
echo   问题D：JSON序列化问题
echo     - 检查是否有JSON序列化错误日志
echo     - 检查文件格式是否正确
echo.

echo 【步骤5：手动验证调试】
echo.
echo   如果没有看到预期的日志输出，说明代码可能没有被执行到：
echo.
echo   A. 检查编辑对话框是否真的调用了SaveContractConfigToFile：
echo      - 寻找"🔧 开始更新合约状态"日志
echo      - 如果没有，说明SaveContractConfigToFile没被调用
echo.
echo   B. 检查状态是否真的被识别为"已执行"：
echo      - 寻找"待更新状态: 保本=✓"日志
echo      - 如果显示的是"保本=-"，说明状态没有正确传递
echo.
echo   C. 检查合约键是否正确生成：
echo      - 寻找"标准化合约键: BTCUSDT_LONG"日志
echo      - 确认键格式是否正确
echo.

echo 🔧 增强日志验证方法：
echo.
echo   由于代码中已经添加了 LogCritical 级别的详细日志，
echo   您应该能看到完整的状态更新流程。
echo.
echo   如果仍然看不到任何相关日志，可能的原因：
echo   1. 代码没有编译最新版本
echo   2. 运行的不是最新编译的程序
echo   3. 日志级别设置过滤了这些信息
echo   4. 异常被捕获但没有正确处理
echo.

echo 🎯 预期的文件变化：
echo.
echo   修改前：
echo   "breakEvenConfig": {
echo     "isEnabled": true,
echo     "triggerProfitAmount": 95.00,
echo     "executionState": 0,           ← NotTriggered
echo     "executionTime": null,         ← 无执行时间
echo     "executionPnl": 0,
echo     "isExecuted": false,           ← 未执行
echo     "executionResult": ""
echo   }
echo.
echo   修改后：
echo   "breakEvenConfig": {
echo     "isEnabled": true,
echo     "triggerProfitAmount": 95.00,
echo     "executionState": 2,           ← Executed
echo     "executionTime": "2024-01-01T12:00:00",  ← 执行时间
echo     "executionPnl": 0,
echo     "isExecuted": true,            ← 已执行
echo     "executionResult": "手动设置为executed"  ← 执行结果
echo   }
echo.

echo 请按照上述步骤仔细检查日志输出，找出问题所在！
echo 特别关注日志中是否出现了 LogCritical 级别的详细状态更新信息。
echo.
pause 