@echo off
chcp 65001 > nul
echo 🔧 保本目标显示修复验证脚本
echo ===================================
echo.

:: 设置时间戳
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set="MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Secs=%dt:~12,2%"
set "datestamp=%YYYY%-%MM%-%DD%" & set "timestamp=%HH%:%Min%:%Secs%"

echo 📅 验证时间: %datestamp% %timestamp%
echo.

echo 🔧 保本目标显示不一致问题修复验证
echo.

echo 📋 问题描述:
echo   用户反馈："保本触发目标与文件内不一致，文件显示9.54，界面显示却是0"
echo   根本原因：测试数据创建时使用硬编码值覆盖了状态文件的实际配置
echo.

echo ✅ 修复内容:
echo.
echo 🔧 修复：优先使用状态文件的实际配置
echo   - 在CreateEnhancedTestContractConfigs方法中添加状态文件读取
echo   - 优先使用existingStates中的实际配置数据
echo   - 调用PopulateConfigFromState方法填充真实数据
echo   - 只有在找不到实际配置时才使用测试默认值
echo.

echo 🧪 验证测试步骤:
echo.
echo 第1步: 检查状态文件内容
echo   1. 打开 ContractMonitoringStates.json 文件
echo   2. 查找保本配置的 triggerProfitAmount 值
echo   3. 记录实际的数值（如：9.54）
echo.
echo 第2步: 启动盯盘面板
echo   1. 打开自动盯盘管理面板
echo   2. 观察合约配置表格中的"保本目标"列
echo   3. 确认显示的数值与文件中一致
echo.
echo 第3步: 验证数据加载日志
echo   启动时应该看到：
echo   📊 从状态文件读取到 X 个实际配置，将优先使用实际数据
echo   ✅ 找到 SYMBOL_SIDE 的实际配置数据，使用文件中的真实值
echo   🔧 已应用 SYMBOL_SIDE 的实际状态数据，保本目标: 9.54
echo.
echo 第4步: 检查界面显示
echo   在合约配置表格中：
echo   ✅ 保本目标列应该显示文件中的实际值（如：9.54）
echo   ✅ 不应该显示0或其他错误值
echo   ✅ 与状态文件中的triggerProfitAmount值完全一致
echo.

echo 🎯 成功指标:
echo.
echo ✅ 状态文件读取: 正确读取实际配置数据
echo ✅ 数据填充: 使用PopulateConfigFromState填充真实值
echo ✅ 界面显示: 保本目标与文件内容完全一致
echo ✅ 日志记录: 显示使用实际配置数据的信息
echo.

echo 🚨 问题排查:
echo.
echo 问题1: 界面仍显示0或错误值
echo   检查日志中是否有：
echo   ✅ 找到 SYMBOL_SIDE 的实际配置数据
echo   如果没有，说明状态文件读取失败
echo.
echo 问题2: 使用了默认测试数据
echo   检查日志中是否有：
echo   ⚠️ 未找到 SYMBOL_SIDE 的实际配置，使用默认测试数据
echo   这说明状态文件中没有对应的配置
echo.
echo 问题3: 状态文件读取异常
echo   检查日志中是否有错误信息：
echo   - 文件不存在
echo   - JSON解析失败
echo   - 配置数据格式问题
echo.

echo 📊 预期的正确日志模式:
echo.
echo 初始化阶段:
echo [HH:MM:SS] 🧪 创建增强的多品种测试配置数据（结合主界面实时持仓）...
echo [HH:MM:SS] 📊 从状态文件读取到 2 个实际配置，将优先使用实际数据
echo.
echo 配置应用阶段:
echo [HH:MM:SS] ✅ 找到 BTCUSDT_LONG 的实际配置数据，使用文件中的真实值
echo [HH:MM:SS] 🔧 已应用 BTCUSDT_LONG 的实际状态数据，保本目标: 9.54
echo [HH:MM:SS] ✅ 找到 ETHUSDT_LONG 的实际配置数据，使用文件中的真实值
echo [HH:MM:SS] 🔧 已应用 ETHUSDT_LONG 的实际状态数据，保本目标: X.XX
echo.

echo 🔍 关键验证点:
echo.
echo 1️⃣ 文件数据读取: existingStates应该包含实际配置
echo 2️⃣ 配置数据应用: PopulateConfigFromState正确执行
echo 3️⃣ 界面数据绑定: BreakEvenTarget属性正确显示
echo 4️⃣ 数据一致性: 界面值与文件值完全匹配
echo.

echo 📝 验证checklist:
echo.
echo □ 状态文件存在且包含配置数据
echo □ 日志显示"从状态文件读取到 X 个实际配置"
echo □ 日志显示"找到 SYMBOL_SIDE 的实际配置数据"
echo □ 日志显示"保本目标: X.XX"（实际数值）
echo □ 界面保本目标列显示正确数值
echo □ 数值与状态文件中triggerProfitAmount一致
echo.

echo 🎉 如果以上所有验证点都通过，说明保本目标显示问题已解决！
echo.
echo 🚀 请现在测试：启动盯盘面板，检查保本目标是否显示正确。
echo.

pause 