@echo off
chcp 65001 > nul
echo 🚫 重复数据修复验证脚本
echo ===================================
echo.

:: 设置时间戳
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Secs=%dt:~12,2%"
set "datestamp=%YYYY%-%MM%-%DD%" & set "timestamp=%HH%:%Min%:%Secs%"

echo 📅 验证时间: %datestamp% %timestamp%
echo.

echo 🔧 重复数据问题修复完成验证
echo.

echo 📋 修复内容:
echo.
echo ✅ 修复1: 防止重复创建测试数据
echo    - 在LoadContractConfigsFromStateFile中添加UI数量检查
echo    - 只在UI为空时才创建新的测试数据
echo    - 避免每次刷新都重新创建数据
echo.
echo ✅ 修复2: 优化Test账户定时器刷新逻辑
echo    - Test账户模拟执行后仅刷新UI显示
echo    - 不重新调用LoadContractConfigsFromStateFile
echo    - 减少不必要的数据重新加载
echo.
echo ✅ 修复3: 分离真实账户和Test账户的刷新策略
echo    - 真实账户：从状态文件重新加载
echo    - Test账户：仅刷新现有UI数据
echo    - 避免混合刷新导致的重复
echo.

echo 🧪 验证测试步骤:
echo.
echo 第1步: 清理环境并重新启动
echo   1. 停止当前盯盘（如果正在运行）
echo   2. 关闭盯盘面板
echo   3. 重新打开盯盘面板
echo   4. 观察初始加载的配置数量
echo.
echo 第2步: 验证初始加载不重复
echo   启动后应该看到:
echo   📊 从状态文件加载到 X 个合约配置
echo   ✅ UI更新完成，当前ContractConfigs数量: X
echo   ❌ 不应该看到重复的合约记录
echo.
echo 第3步: 启动Test账户盯盘
echo   1. 确认账户为"Test"
echo   2. 点击"启动盯盘"
echo   3. 观察启动后的配置数量是否与启动前一致
echo.
echo 第4步: 验证定时器刷新不增加重复数据
echo   启动盯盘后每5秒应该看到:
echo   ⏰【模拟扫描】开始Test账户模拟条件检查...
echo   ✅【模拟扫描】Test账户模拟条件检查完成，UI已刷新
echo   ❌ 不应该看到"📊 已添加到UI"重复出现
echo.
echo 第5步: 验证UI表格显示无重复
echo   在界面的合约配置表格中:
echo   ✅ 每个合约应该只显示一行
echo   ✅ TONUSDT、DMCUSDT等不应该重复出现
echo   ❌ 不应该有相同的合约显示多次
echo.

echo 🎯 成功指标:
echo.
echo ✅ 界面初始加载: 只显示唯一的合约记录
echo ✅ 启动盯盘后: 配置数量保持不变
echo ✅ 定时器刷新: 不增加新的重复记录
echo ✅ UI表格显示: 每个合约只有一行
echo ✅ 日志信息: 不重复出现"📊 已添加到UI"
echo.

echo 🚨 问题排查:
echo.
echo 问题1: 仍然有重复记录
echo   检查日志中是否有:
echo   🔒 【防重复】UI已有 X 个配置，跳过测试数据创建
echo   如果没有此日志，说明防重复逻辑未生效
echo.
echo 问题2: Test账户每5秒都增加记录
echo   检查日志应该显示:
echo   ✅【模拟扫描】Test账户模拟条件检查完成，UI已刷新
echo   而不是:
echo   📊 已添加到UI: XXX （这说明重新加载了数据）
echo.
echo 问题3: 真实账户也有重复
echo   真实账户应该走完整的状态文件重新加载
echo   但初始状态不应该重复
echo.

echo 📊 预期的正确日志模式:
echo.
echo 初始加载（仅一次）:
echo [HH:MM:SS] 📊 从状态文件加载到 2 个合约配置
echo [HH:MM:SS] ✅ UI更新完成，当前ContractConfigs数量: 2
echo.
echo Test账户模拟扫描（每5秒）:
echo [HH:MM:SS] ⏰【模拟扫描】开始Test账户模拟条件检查...
echo [HH:MM:SS] 🔍【模拟检查】TONUSDT BOTH: 当前浮盈 XXX.XXU
echo [HH:MM:SS] ✅【模拟扫描】Test账户模拟条件检查完成，UI已刷新
echo.
echo 防重复保护（应该看到）:
echo [HH:MM:SS] 🔒 【防重复】UI已有 2 个配置，跳过测试数据创建
echo.

echo 🔍 关键验证点:
echo.
echo 1️⃣ 界面表格: 合约记录数量应该是唯一的
echo 2️⃣ 日志计数: ContractConfigs数量应该保持稳定
echo 3️⃣ 防重复日志: 应该看到跳过测试数据创建的日志
echo 4️⃣ 模拟扫描: Test账户应该只刷新UI，不重新加载数据
echo.

echo 🎉 如果以上所有验证点都通过，说明重复数据问题已解决！
echo.
echo 🚀 请现在测试：重新启动盯盘面板，观察配置数量的变化。
echo.

pause 