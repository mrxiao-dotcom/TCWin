@echo off
chcp 65001 >nul
echo.
echo ================================
echo 简化自动盯盘启动流程修复
echo ================================
echo.

echo 🔧 正在应用修复...

REM 检查文件是否存在
if not exist "Views\AutoMonitorDashboard.xaml.cs" (
    echo ❌ 错误：找不到 Views\AutoMonitorDashboard.xaml.cs 文件
    echo 请确保在项目根目录下运行此脚本
    pause
    exit /b 1
)

echo ✅ 找到目标文件

REM 创建备份
echo 📋 创建备份文件...
copy "Views\AutoMonitorDashboard.xaml.cs" "Views\AutoMonitorDashboard.xaml.cs.backup_%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%" >nul 2>&1

echo.
echo 🔧 需要手动应用以下修改：
echo.
echo ----------------------------------------
echo 修改1: LoadCurrentPositionsWithConfigs 方法（第372-383行）
echo ----------------------------------------
echo 将以下复杂的错误提示：
echo.
echo MessageBox.Show("❌ 步骤2失败：查找当前持仓\n\n" +
echo     "未找到当前活跃持仓！\n\n" +
echo     "🔧 可能的原因：\n" +
echo     "1. AutoMonitorService 未初始化或无持仓档案\n" +
echo     "2. MainViewModel 中无持仓数据\n" +
echo     "3. 账户中确实没有未平仓的合约持仓\n\n" +
echo     "💡 建议：\n" +
echo     "• 请先在主界面查看是否有活跃持仓\n" +
echo     "• 确保已连接到币安账户\n" +
echo     "• 确认AutoMonitorService正常运行\n\n" +
echo     "📋 正确的载入流程：\n" +
echo     "1. ✅ 获取基础配置\n" +
echo     "2. ❌ 查找当前持仓 ← 当前步骤失败\n" +
echo     "3. ⏸️ 根据基础配置生成缺失配置\n" +
echo     "4. ⏸️ 更新触发值，保持执行状态", 
echo     "载入失败 - 步骤2", MessageBoxButton.OK, MessageBoxImage.Warning);
echo return;
echo.
echo 替换为：
echo.
echo _logger.LogInformation("📊 未找到活跃持仓，创建示例数据以便用户查看界面");
echo // 🔧 优化：没有持仓时直接创建示例数据，不阻止用户
echo CreateExampleDataBasedOnConfig(baseConfig);
echo // 简化的友好提示
echo AppendLog("💡 当前没有活跃持仓，显示示例数据。开仓后可点击【刷新配置】载入真实持仓");
echo // 更新界面
echo UpdateNewInterfaceStats();
echo return;
echo.
echo ----------------------------------------
echo 修改2: 简化成功提示（第420-435行）
echo ----------------------------------------
echo 将复杂的MessageBox成功提示替换为简单的AppendLog：
echo.
echo AppendLog($"✅ 载入完成！成功载入 {ContractMonitors.Count} 个合约，{ContractMonitors.Sum(c => c.TriggerConditions.Count)} 个监控条件");
echo.
echo ----------------------------------------
echo 修改3: LoadFromConfigButton_Click 方法（第2193-2199行）
echo ----------------------------------------
echo 将复杂的配置提醒MessageBox简化为日志记录
echo.

echo.
echo 🚀 修复说明：
echo 1. 移除所有阻塞性的错误提示弹出框
echo 2. 使用日志记录代替复杂的用户交互
echo 3. 当没有配置或持仓时，自动创建可用的示例数据
echo 4. 保持所有功能完整，只是简化用户体验
echo.
echo 📝 建议使用代码编辑器手动应用这些修改
echo 因为涉及多处代码替换，自动脚本可能影响其他代码
echo.

echo ✅ 修复指导完成！
echo.
echo 修改完成后请测试以下场景：
echo 1. 首次启动（无配置无持仓）
echo 2. 有配置但无持仓
echo 3. 正常启动（有配置有持仓）
echo.

pause 