@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================
echo 🔍 调试界面创建问题
echo ============================

echo.
echo ✅ 最新修复内容：
echo ────────────────────────────────
echo 🔧 错误处理优化：
echo   • 移除了CreateCodeBasedUI中的fallback回退
echo   • 添加了详细的步骤日志输出
echo   • 对每个UI组件进行独立异常处理
echo   • 左侧面板失败时显示占位符而不是崩溃

echo 🔧 分步创建机制：
echo   • CreateTitlePanel()：独立创建标题栏
echo   • CreateStatusCardsPanel()：状态卡片，失败时跳过
echo   • CreateLeftPanel()：动态表格，失败时显示占位符
echo   • CreateRightPanel()：执行历史，失败时跳过

echo.
echo 🎯 关键日志监控：
echo ────────────────────────────────
echo 成功路径应看到：
echo   • "🎯 开始创建代码化UI界面"
echo   • "📝 窗口基本属性设置完成"
echo   • "📝 主网格结构创建完成"
echo   • "📝 标题栏创建完成"
echo   • "📝 状态卡片区域创建完成"
echo   • "📝 左侧面板创建完成"
echo   • "📝 右侧面板创建完成"
echo   • "✅ 代码化UI界面创建成功"

echo 失败路径会看到：
echo   • "❌ 状态卡片创建失败，跳过"
echo   • "❌ 左侧面板创建失败"
echo   • "❌ 右侧面板创建失败，跳过"
echo   • "❌ 增强版代码UI创建失败"

echo.
echo 🚨 现在的诊断策略：
echo ────────────────────────────────
echo 情况1：如果看到完整界面但没有表格
echo   • 日志应显示"左侧面板创建完成"
echo   • 但可能InitializeBasicDataGridColumns()失败
echo   • 查看"🎯 初始化基础DataGrid列结构"相关日志

echo 情况2：如果看到"动态表格加载失败"占位符
echo   • 说明CreateLeftPanel()中有异常
echo   • 查看"❌ 左侧面板创建失败"详细错误信息
echo   • 可能是CreateContractMonitorDataGrid()问题

echo 情况3：如果还是简单文本
echo   • 说明CreateCodeBasedUI()整体失败
echo   • 查看"❌ 增强版代码UI创建失败"错误
echo   • 可能是基础控件创建有问题

echo.
echo 🔍 测试步骤：
echo ────────────────────────────────
echo 1. 启动程序（下面会自动启动）
echo 2. 观察控制台日志输出
echo 3. 点击【自动盯盘】
echo 4. 根据看到的界面形态判断问题：

echo.
echo   📋 预期结果A：完整的专业界面
echo   ┌─────────────────────────────────────────┐
echo   │ 🔍 自动盯盘监控面板                      │
echo   │ 🧹清理状态  🔄刷新  ❌关闭             │
echo   ├─────────────────────────────────────────┤
echo   │ 📊 状态卡片区域                         │
echo   ├─────────────────────────────────────────┤
echo   │ 🎯 合约触发条件管理    │ 📈 最近执行历史 │
echo   │ [动态表格数据]          │ [历史记录]      │
echo   └─────────────────────────────────────────┘

echo.
echo   📋 备选结果B：部分界面+占位符
echo   ┌─────────────────────────────────────────┐
echo   │ 🔍 自动盯盘监控面板                      │
echo   │ 🧹清理状态  🔄刷新  ❌关闭             │
echo   ├─────────────────────────────────────────┤
echo   │ 📊 状态卡片区域                         │
echo   ├─────────────────────────────────────────┤
echo   │ 动态表格加载失败        │ 📈 最近执行历史 │
echo   │ [灰色占位符]           │ [历史记录]      │
echo   └─────────────────────────────────────────┘

echo.
echo   📋 失败结果C：简单文本（需进一步修复）
echo   ┌─────────────────────────────────────────┐
echo   │          自动盯盘监控面板                │
echo   │                                        │
echo   │        状态：正在运行                   │
echo   │                                        │
echo   │  如需查看详细信息，请检查主界面的       │
echo   │      自动盯盘状态。                     │
echo   └─────────────────────────────────────────┘

echo.
echo ============================
echo 🎯 启动调试测试
echo ============================

start "" "bin/Debug/net6.0-windows/BinanceFuturesTrader.exe"

echo ✅ 程序已启动，请：
echo 1. 观察控制台日志输出（特别注意上述关键日志）
echo 2. 点击【自动盯盘】按钮
echo 3. 根据看到的界面判断问题类型
echo 4. 将具体现象和相关日志反馈给我

echo.
echo 📝 请告诉我：
echo   • 看到了哪种界面类型（A/B/C）？
echo   • 控制台显示了哪些关键日志？
echo   • 是否有具体的错误信息？

echo.
pause 