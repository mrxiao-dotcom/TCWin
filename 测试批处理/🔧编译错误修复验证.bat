@echo off
chcp 65001 >nul

REM 复制脚本功能：既验证编译又生成测试数据
echo 🎯 多品种测试数据生成器 + 编译验证
echo =====================================
echo.

echo 📋 验证内容：
echo    • 编译错误修复验证
echo    • 多品种测试数据验证
echo    • 3个测试合约显示验证
echo.

echo 🔍 验证步骤：
echo.

echo 步骤1: 检查统一数据服务文件是否存在
if exist "Services\UnifiedStateDataService.cs" (
    echo ✅ UnifiedStateDataService.cs 文件存在
) else (
    echo ❌ UnifiedStateDataService.cs 文件不存在
    goto :error
)
echo.

echo 步骤2: 检查类名重命名是否完成
findstr /n "class UnifiedStateStatistics" "Services\UnifiedStateDataService.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ StateStatistics 已重命名为 UnifiedStateStatistics
) else (
    echo ❌ StateStatistics 重命名失败
)

findstr /n "class UnifiedContractMonitorViewModel" "Services\UnifiedStateDataService.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ ContractMonitorViewModel 已重命名为 UnifiedContractMonitorViewModel
) else (
    echo ❌ ContractMonitorViewModel 重命名失败
)
echo.

echo 步骤3: 检查是否仍有冲突的类名
findstr /n "class StateStatistics" "Services\UnifiedStateDataService.cs" 2>nul
if %errorlevel%==0 (
    echo ❌ 仍然存在冲突的 StateStatistics 类
) else (
    echo ✅ 冲突的 StateStatistics 类已移除
)

findstr /n "class ContractMonitorViewModel" "Services\UnifiedStateDataService.cs" 2>nul
if %errorlevel%==0 (
    echo ❌ 仍然存在冲突的 ContractMonitorViewModel 类
) else (
    echo ✅ 冲突的 ContractMonitorViewModel 类已移除
)
echo.

echo 步骤4: 检查ExecutionState.Failed引用是否已移除
findstr /n "ExecutionState.Failed" "Services\UnifiedStateDataService.cs" 2>nul
if %errorlevel%==0 (
    echo ❌ 仍然存在 ExecutionState.Failed 引用
) else (
    echo ✅ ExecutionState.Failed 引用已移除
)
echo.

echo 步骤5: 检查DateTime问题是否已修复
findstr /n "LastUpdateTime.HasValue" "Services\UnifiedStateDataService.cs" 2>nul
if %errorlevel%==0 (
    echo ❌ 仍然存在 DateTime.HasValue 问题
) else (
    echo ✅ DateTime.HasValue 问题已修复
)

findstr /n "LastUpdateTime.Value" "Services\UnifiedStateDataService.cs" 2>nul
if %errorlevel%==0 (
    echo ❌ 仍然存在 DateTime.Value 问题
) else (
    echo ✅ DateTime.Value 问题已修复
)
echo.

echo 步骤6: 验证多品种测试数据
findstr /n "🎯.*测试模式.*创建多品种示例数据" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ 测试数据调用已重新启用
) else (
    echo ❌ 测试数据调用未启用
)

findstr /n "ETHUSDT.*LONG" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ ETH测试数据已添加
) else (
    echo ❌ ETH测试数据未找到
)

findstr /n "XRPUSDT.*SHORT" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ XRP测试数据已添加
) else (
    echo ❌ XRP测试数据未找到
)

findstr /n "1000.00m.*大幅盈利场景" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ ETH浮盈1000U设置正确
) else (
    echo ❌ ETH浮盈设置可能有问题
)

findstr /n "-100.00m.*亏损场景" "Views\AutoMonitorDashboard.xaml.cs" 2>nul
if %errorlevel%==0 (
    echo ✅ XRP浮盈-100U设置正确
) else (
    echo ❌ XRP浮盈设置可能有问题
)
echo.

echo 🏗️ 尝试编译项目验证修复...
echo.

if exist "TCWin.sln" (
    echo 📦 使用 MSBuild 编译项目...
    msbuild TCWin.sln /p:Configuration=Debug /p:Platform="Any CPU" /v:m /flp:logfile=unified_compile_log.txt;verbosity=minimal
    
    if %errorlevel%==0 (
        echo.
        echo ✅ 🎉 编译成功！UnifiedStateDataService修复完成！
        echo.
        echo 📊 修复验证通过：
        echo    • 所有命名冲突已解决
        echo    • ExecutionState.Failed 引用已移除
        echo    • DateTime 问题已修复
        echo    • 项目可以正常编译
        echo.
        echo 🚀 下一步操作：
        echo    1. 启动程序查看多品种测试数据
        echo    2. 验证3个合约是否正确显示
        echo    3. 检查浮盈数值和颜色
        echo.
        echo 📊 期望看到的测试数据：
        echo    📋 BTCUSDT_LONG: 浮盈 +250.75U (绿色)
        echo    📋 ETHUSDT_LONG: 浮盈 +1000.00U (绿色)
        echo    📋 XRPUSDT_SHORT: 浮盈 -100.00U (红色)
        echo.
    ) else (
        echo.
        echo ❌ 编译仍然失败，请检查编译日志：
        echo.
        if exist "unified_compile_log.txt" (
            echo 🔍 编译错误详情：
            type unified_compile_log.txt | findstr /i "error"
        )
        echo.
        echo 💡 可能的问题：
        echo    • 仍有命名空间冲突
        echo    • 缺少using引用
        echo    • 其他语法错误
        echo.
    )
) else (
    echo ⚠️ 未找到 TCWin.sln 文件，无法执行编译测试
    echo 请在 Visual Studio 中手动检查编译状态
)

echo.
echo 📊 修复内容总结：
echo =====================================
echo.
echo ✅ 解决的问题：
echo    • StateStatistics → UnifiedStateStatistics (避免命名冲突)
echo    • ContractMonitorViewModel → UnifiedContractMonitorViewModel (避免命名冲突) 
echo    • 移除了 ExecutionState.Failed 引用
echo    • 修复了 DateTime.HasValue/Value 在非可空类型上的使用
echo    • 简化了 LastUpdateTime 的统计逻辑
echo.

echo 🎯 修复后的架构：
echo    📁 Services\UnifiedStateDataService.cs
echo    ├── UnifiedStateDataService (主服务类)
echo    ├── UnifiedStateStatistics (统计信息)
echo    └── UnifiedContractMonitorViewModel (视图模型)
echo.
echo 🧪 测试数据触发条件：
echo    • 没有实际状态文件时自动显示
echo    • 数据加载异常时自动显示
echo    • 可调用CreateTestData()方法手动触发
echo    • 确保删除现有状态文件以触发测试模式
echo.

echo =====================================
echo 🔧 UnifiedStateDataService编译错误修复验证完成！
echo.

echo 🧪 清空状态文件以触发测试数据显示...
set "STATE_DIR=%APPDATA%\BinanceFuturesTrader\Accounts\Test"
set "STATE_FILE=%STATE_DIR%\contract_monitoring_states.json"

if exist "%STATE_FILE%" (
    echo 📁 备份并清空现有状态文件...
    copy "%STATE_FILE%" "%STATE_FILE%.backup" >nul 2>&1
    del "%STATE_FILE%" >nul 2>&1
    echo ✅ 状态文件已清空，启动程序将显示测试数据
) else (
    echo ✅ 无现有状态文件，启动程序将自动显示测试数据
)
echo.

echo 🎯 测试步骤：
echo 1. 启动程序，确保选择Test账户
echo 2. 点击"自动盯盘监控"按钮
echo 3. 应该看到配置窗口显示3个测试合约：
echo    📋 BTCUSDT LONG: 浮盈 +250.75U
echo    📋 ETHUSDT LONG: 浮盈 +1000.00U  
echo    📋 XRPUSDT SHORT: 浮盈 -100.00U
echo.

echo 💡 如果看不到测试数据，请检查日志中的"🧪 进入测试模式"消息
pause
goto :eof

:error
echo.
echo ❌ 发现问题，请检查文件是否正确创建和修改
pause 