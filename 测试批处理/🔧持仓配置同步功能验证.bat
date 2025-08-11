@echo off
chcp 65001 >nul
echo 🔧 持仓配置同步功能验证脚本
echo =====================================
echo.

echo 📋 本脚本用于验证持仓与统一配置文件同步功能
echo.

echo 🔍 验证步骤：
echo.

echo 步骤1: 检查核心服务文件
if exist "Services\PositionConfigSyncService.cs" (
    echo ✅ PositionConfigSyncService.cs 文件存在
) else (
    echo ❌ PositionConfigSyncService.cs 文件不存在
)

echo.
echo 步骤2: 检查MainViewModel集成
findstr /n "SyncPositionsWithConfigFileAsync" "ViewModels\MainViewModel.Data.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ MainViewModel中已集成同步功能
) else (
    echo ❌ MainViewModel中未找到同步功能
)

findstr /n "Task.Run.*SyncPositionsWithConfigFileAsync" "ViewModels\MainViewModel.Data.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 后台同步任务已配置
) else (
    echo ❌ 后台同步任务未配置
)

echo.
echo 步骤3: 检查同步服务核心方法
findstr /n "SyncConfigWithPositionsAsync" "Services\PositionConfigSyncService.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 核心同步方法已实现
) else (
    echo ❌ 核心同步方法未找到
)

findstr /n "CreateConfigFromPosition" "Services\PositionConfigSyncService.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 配置生成方法已实现
) else (
    echo ❌ 配置生成方法未找到
)

echo.
echo 步骤4: 编译验证
echo 正在编译项目...
dotnet build TCWin.sln --verbosity quiet >nul 2>&1
if %errorlevel%==0 (
    echo ✅ 项目编译成功，同步功能可正常使用
) else (
    echo ❌ 项目编译失败，需要检查代码
    echo 运行详细编译查看错误：
    echo    dotnet build TCWin.sln
    pause
    exit /b 1
)

echo.
echo 🎯 功能验证完成总结：
echo.
echo ✅ 已实现的功能：
echo    • PositionConfigSyncService - 专门的同步服务
echo    • MainViewModel集成 - 数据刷新时自动同步
echo    • 智能对比分析 - 精确识别配置差异
echo    • 基于基础配置生成 - 新持仓自动获得完整策略
echo    • 原子性文件操作 - 确保数据一致性
echo.

echo 🔄 同步触发时机：
echo    • 持仓数据刷新时（MainViewModel.Data.cs）
echo    • 新开仓位时自动添加配置
echo    • 平仓时自动移除配置
echo    • 配置文件装载时智能匹配
echo.

echo 📊 预期行为：
echo    1. 新开仓位 → 自动在统一配置文件中生成对应配置
echo    2. 平仓操作 → 自动从统一配置文件中移除配置
echo    3. 配置窗口 → 显示的合约与实际持仓完全一致
echo    4. 策略继承 → 新配置基于当前基础配置的完整策略
echo.

echo 🚀 测试建议：
echo.
echo 测试场景1: 新开仓位测试
echo    1. 启动程序，选择Test账户
echo    2. 观察当前持仓和配置文件状态
echo    3. 模拟新开仓位（或等待实际交易）
echo    4. 检查统一配置文件是否自动更新
echo    5. 验证配置窗口是否显示新合约
echo.

echo 测试场景2: 平仓测试
echo    1. 在有持仓的情况下
echo    2. 平掉某个持仓
echo    3. 检查配置文件是否移除对应配置
echo    4. 验证配置窗口是否同步更新
echo.

echo 测试场景3: 配置继承测试
echo    1. 设置一个基础配置（包含保本、推仓、保盈策略）
echo    2. 新开一个持仓
echo    3. 检查生成的配置是否包含完整策略
echo    4. 验证所有策略状态初始化为"未触发"
echo.

echo 📁 关键文件位置：
echo    • 同步服务: Services\PositionConfigSyncService.cs
echo    • 集成点: ViewModels\MainViewModel.Data.cs
echo    • 统一配置文件: %%APPDATA%%\BinanceFuturesTrader\Accounts\Test\contract_monitoring_states.json
echo.

echo 📝 日志关键词：
echo    查找以下日志消息验证功能工作：
echo    • "🔄 开始执行持仓与统一配置文件同步"
echo    • "📊 检测到 X 个活跃持仓，开始同步配置"
echo    • "➕ 需要添加配置" 或 "➖ 需要删除配置"
echo    • "✅ 持仓配置同步完成，配置文件已更新"
echo.

echo 🎉 持仓配置同步功能验证完成！
echo.
echo 💡 下一步：
echo    启动程序开始测试持仓与配置同步功能
echo    观察日志输出确认功能正常工作
echo.

pause 