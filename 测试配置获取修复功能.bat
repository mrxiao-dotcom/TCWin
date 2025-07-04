@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================
echo 🔧 测试配置获取修复功能
echo ============================

echo.
echo 📝 本次修复内容：
echo   ✅ AutoMonitorDashboard现在可以从MainViewModel获取配置
echo   ✅ 新增MainViewModel.CurrentAutoMonitorConfig属性
echo   ✅ 新增MainViewModel.GetAccountAutoMonitorConfigs()方法
echo   ✅ 配置获取失败时的智能降级逻辑

echo.
echo 🎯 测试步骤：
echo 1. 启动程序，选择账户
echo 2. 点击【盯盘参数配置】，设置参数并保存
echo 3. 点击【自动盯盘】打开监控面板
echo 4. 验证以下内容：

echo.
echo ✅ 应该看到的改进效果：
echo   • 不再显示"未找到当前配置"的错误提示
echo   • 监控面板能正常加载配置的保本、推仓、止盈设置
echo   • 表格中显示对应的触发条件和状态
echo   • 配置信息在监控面板中正确显示

echo.
echo 🔍 重点验证项：
echo   • 配置保存后立即打开监控面板能正常显示
echo   • 监控面板标题显示正确的配置名称
echo   • 表格列根据配置动态生成（保本+推仓阶梯+止盈阶梯）
echo   • 状态统计卡片显示正确的条件数量

echo.
echo 🚨 如果仍有问题，检查：
echo   • 是否成功保存了配置
echo   • MainViewModel.CurrentAutoMonitorConfig是否有值
echo   • 日志中是否有相关错误信息

echo.
echo ============================
echo 🚀 启动程序进行测试
echo ============================

echo 启动程序...
start "" "bin\Debug\net6.0-windows\BinanceFuturesTrader.exe"

echo.
echo 📋 程序已启动，请按照上述步骤进行测试
echo 如有问题请截图反馈具体现象
pause 