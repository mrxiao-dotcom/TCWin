@echo off
chcp 65001 > nul
echo.
echo 🔧 启动盯盘面板状态一致性修复验证
echo ==========================================
echo.

echo 📋 修复内容总结:
echo   1. 修复MainViewModel在打开面板前优先检查统一状态文件
echo   2. 修复AutoMonitorConfigWindow在Loaded时重新加载统一状态文件
echo   3. 确保界面显示与文件内容完全一致
echo.

echo 🔍 检查修复效果:
echo   1. 编译项目...
dotnet build BinanceFuturesTrader.csproj --configuration Debug --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo ❌ 编译失败，请检查代码错误
    pause
    exit /b 1
)
echo ✅ 编译成功

echo.
echo   2. 检查统一状态文件...
set "stateFile=Data\contract_monitoring_states.json"
if exist "%stateFile%" (
    echo ✅ 找到统一状态文件: %stateFile%
    echo 📄 文件大小: 
    for %%A in ("%stateFile%") do echo    %%~zA 字节
    echo 📄 修改时间:
    for %%A in ("%stateFile%") do echo    %%~tA
    echo.
    echo 📖 文件内容预览 (前800字符):
    powershell -command "Get-Content '%stateFile%' -Raw | Select-Object -First 1 | ForEach-Object { $_.Substring(0, [Math]::Min(800, $_.Length)) }"
    echo.
    echo 📊 解析JSON结构:
    powershell -command "$json = Get-Content '%stateFile%' -Raw | ConvertFrom-Json; Write-Host '合约状态数量:' $json.PSObject.Properties.Count; $json.PSObject.Properties | ForEach-Object { Write-Host '  - ' $_.Name ':' $_.Value.Symbol $_.Value.PositionSide '(配置:' $_.Value.BaseConfigName ')' }"
) else (
    echo ⚠️ 统一状态文件不存在: %stateFile%
    echo 💡 这可能是正常的，如果还没有运行过监控
    echo 💡 请先设置一些合约配置来生成状态文件
)

echo.
echo   3. 检查源代码修复...
echo 🔍 检查MainViewModel的统一状态文件检查逻辑:
findstr /n "统一状态文件路径" "ViewModels\MainViewModel.AutoMonitor.cs"
if %ERRORLEVEL% equ 0 (
    echo ✅ 找到统一状态文件检查代码
) else (
    echo ⚠️ 未找到预期的统一状态文件检查代码
)

echo.
echo 🔍 检查AutoMonitorConfigWindow的Loaded事件修复:
findstr /n "LoadExistingContractConfigsAsync" "Views\AutoMonitorConfigWindowSimple.xaml.cs" | findstr "Loaded" -A 3 -B 3
if %ERRORLEVEL% equ 0 (
    echo ✅ 找到Loaded事件中的统一状态文件加载代码
) else (
    echo ⚠️ 未找到预期的Loaded事件修复
)

echo.
echo   4. 启动应用程序进行实际测试...
echo 💡 请按照以下步骤进行验证:
echo.
echo 🎯 测试步骤:
echo   a) 确保之前已经设置过合约配置（统一状态文件中有数据）
echo   b) 打开应用程序主界面
echo   c) 点击"自动盯盘"按钮
echo   d) 观察配置窗口是否正确显示统一状态文件中的配置
echo   e) 检查日志中是否有正确的加载顺序
echo.

echo 🚀 启动应用程序...
start "" "bin\Debug\net6.0-windows\BinanceFuturesTrader.exe"

echo.
echo 📊 验证要点指南:
echo ==========================================
echo.
echo 🎯 验证目标1: 主窗口打开面板前的状态检查
echo   应该看到日志:
echo   - "🔍【面板打开】统一状态文件路径: ..."
echo   - "🔍【面板打开】统一状态文件是否存在: True"
echo   - "🔍【面板打开】统一状态文件中发现 X 个合约状态"
echo   - "✅【面板打开】统一状态文件包含有效数据，将优先使用"
echo.
echo 🎯 验证目标2: 配置窗口的数据加载顺序
echo   应该看到日志顺序:
echo   1. "🔄 窗口加载完成，正在刷新最新数据..."
echo   2. "🔄 重新检查统一状态文件，确保与文件内容一致..."
echo   3. "✅ 找到状态文件，强制从文件重新加载最新数据..."
echo   4. "📊 从状态文件加载到 X 个合约配置"
echo   5. "✅ 已从状态文件加载 X 个合约配置到UI"
echo   6. "🔄 更新实时价格和持仓信息..."
echo.
echo 🎯 验证目标3: 界面显示一致性
echo   - 配置窗口中的合约状态应该与统一状态文件完全一致
echo   - 保本状态、推仓状态、保盈状态都应该正确显示
echo   - 不应该出现空白或不匹配的情况
echo.

echo 🔧 如果仍有不一致，请检查:
echo   1. 统一状态文件的JSON格式是否正确
echo   2. 账户名称是否匹配
echo   3. 文件权限是否正确
echo   4. 查看详细的应用程序日志
echo.

pause
echo.
echo 🎉 验证完成！如果界面显示与统一状态文件一致，说明修复成功。 