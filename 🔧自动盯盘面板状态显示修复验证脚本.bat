@echo off
chcp 65001 > nul
echo 🔧 自动盯盘面板状态显示修复验证脚本
echo.

echo 📋 问题描述：
echo   当文件内状态值为2（已执行），打开自动盯盘面板，合约状态配置页面显示还是"-"
echo   但双击在弹出窗口，看到状态就是"已执行"了
echo   根本原因：RefreshPositionDataAsync覆盖了从状态文件正确加载的状态
echo.

echo 🔍 修复内容：
echo.

echo 【修复1：初始加载逻辑优化】
echo   文件：Views/AutoMonitorConfigWindowSimple.xaml.cs
echo   原来：先加载状态文件，再调用RefreshPositionDataAsync（覆盖状态）
echo   修复后：先加载状态文件，再调用RefreshPositionDataWithoutOverridingStatesAsync（保护状态）
echo.

echo 【修复2：新增状态保护方法】
echo   新方法：RefreshPositionDataWithoutOverridingStatesAsync()
echo   功能：只更新实时数据（PNL、时间），不更新状态
echo   保护：已从状态文件加载的正确状态不被覆盖
echo.

echo 【修复3：状态保护机制】
echo   - 对于已存在的配置：只更新 CurrentPnl 和 UpdateTime
echo   - 对于新持仓：正常初始化状态
echo   - 对于已平仓：正常移除配置
echo   - 保护范围：保本状态、推仓状态、保盈状态
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
echo   3. 记录具体的保本、推仓状态值
echo.

echo 【步骤3：测试主界面显示】
echo   1. 打开"启动盯盘面板"
echo   2. 检查合约状态配置页面的状态显示
echo   3. 应该看到"√"（已执行）而不是"-"（未触发）
echo   4. 查看日志输出
echo.

echo 【步骤4：验证编辑窗口一致性】
echo   1. 双击合约配置行打开编辑窗口
echo   2. 确认编辑窗口中的状态显示
echo   3. 主界面和编辑窗口应该显示一致
echo   4. 都应该显示"√"（已执行）
echo.

echo 📊 预期结果：
echo.

echo 【场景1：executionState: 2 (已执行)】
echo   文件中: "executionState": 2
echo   主界面显示: "√" (已执行)
echo   编辑窗口显示: "√" (已执行)
echo   日志: 📊 更新实时数据: BTCUSDT LONG (PNL: XX, 状态保护)
echo.

echo 【场景2：executionState: 1 (执行中)】
echo   文件中: "executionState": 1
echo   主界面显示: "⚡" (执行中)
echo   编辑窗口显示: "⚡" (执行中)
echo   日志: 保护了 X 个配置的状态
echo.

echo 【场景3：executionState: 0 (未触发)】
echo   文件中: "executionState": 0
echo   主界面显示: "-" (未触发)
echo   编辑窗口显示: "-" (未触发)
echo   日志: 状态保护机制正常工作
echo.

echo 🔍 关键验证日志：
echo.

echo 【初始加载日志】
echo   ✅ 找到状态文件，从文件加载数据...
echo   📊 从状态文件加载到 X 个合约配置
echo   ✅ 已加载合约配置: BTCUSDT_LONG
echo      📊 保本状态: √ (ExecutionState: Executed)
echo      📊 推仓阶梯1状态: √ (ExecutionState: Executed)
echo   🔄 正在刷新最新持仓数据（保护状态）...
echo   📊 更新实时数据: BTCUSDT LONG (PNL: 95.50, 状态保护)
echo   ✅ 持仓数据刷新完成，保护了 1 个配置的状态
echo   ✅ 最新持仓数据刷新完成（状态已保护）
echo.

echo 【状态保护验证】
echo   - 主界面保本状态应显示: √
echo   - 主界面推仓状态应显示: √
echo   - 编辑窗口状态应显示: 已执行
echo   - 状态文件与界面显示完全一致
echo.

echo 🎉 修复验证要点：
echo   ✅ 主界面状态显示与状态文件一致
echo   ✅ 编辑窗口状态显示与状态文件一致
echo   ✅ 主界面和编辑窗口状态显示一致
echo   ✅ 状态保护机制正常工作
echo   ✅ 实时数据正常更新但状态被保护
echo.

echo 💡 修复完成！
echo   现在自动盯盘面板的主界面能够正确显示状态文件中的执行状态了
echo   不会再出现文件中是"已执行"但界面显示"未触发"的问题
echo. 