@echo off
chcp 65001 >nul
echo ============================
echo ✅ 测试自动盯盘数据加载功能
echo ============================
echo.
echo 📋 本次功能验证：
echo   ✅ 数据加载功能 - LoadContractMonitorsFromService
echo   ✅ 状态刷新功能 - RefreshContractMonitorStatus  
echo   ✅ 统计信息更新 - UpdateNewInterfaceStats
echo   ✅ 触发条件状态管理 - TriggerExecutionStatus
echo.
echo 🧪 测试步骤：
echo 1. 启动程序，配置API账号
echo 2. 进入盯盘参数配置，设置监控参数
echo 3. 点击"自动盯盘"进入新控制面板
echo 4. 点击"📥 从配置加载"按钮
echo 5. 验证合约数据是否正确加载
echo 6. 点击"🔄 刷新"按钮验证状态更新
echo.
echo ✅ 预期效果：
echo   • 加载后显示合约表格
echo   • 状态卡片显示正确的统计信息
echo   • 示例合约（无持仓时）或真实合约数据
echo   • 保本、推仓、止盈条件正确分类
echo   • 触发状态正确显示（未触发/执行中/已执行）
echo   • 刷新功能实时更新状态
echo.
echo 🔧 新增关键功能：
echo   • 支持从AutoMonitorService加载持仓档案
echo   • 触发条件分类管理（保本/推仓/止盈）
echo   • 状态流转（未触发→执行中→已执行）
echo   • 实时刷新不重新创建数据
echo   • 统计信息自动计算
echo.
echo ============================
echo 🚀 启动程序测试
echo ============================
echo.
start "" "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe"
echo ✅ 程序已启动，请按照上述步骤进行测试
echo.
echo 💡 测试重点：
echo   1. "从配置加载"功能是否正常工作
echo   2. 表格数据是否正确显示
echo   3. 状态卡片统计是否准确
echo   4. 刷新功能是否更新状态
echo.
echo 如有问题请截图反馈具体现象
pause 