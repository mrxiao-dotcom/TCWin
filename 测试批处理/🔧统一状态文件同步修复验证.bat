@echo off
chcp 65001 > nul
echo.
echo 🔧 统一状态文件同步和线程访问修复验证
echo ============================================
echo.

echo 📋 修复内容总结:
echo   1. 修复RefreshPositionDataAsync中的线程访问冲突
echo   2. 确保界面初始化时正确加载统一状态文件
echo   3. 增强异常处理和重试机制
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
echo   2. 检查统一状态文件是否存在...
set "stateFile=Data\contract_monitoring_states.json"
if exist "%stateFile%" (
    echo ✅ 找到状态文件: %stateFile%
    echo 📄 文件大小: 
    for %%A in ("%stateFile%") do echo    %%~zA 字节
    echo 📄 修改时间:
    for %%A in ("%stateFile%") do echo    %%~tA
    echo.
    echo 📖 文件内容预览 (前500字符):
    powershell -command "Get-Content '%stateFile%' -Raw | Select-Object -First 1 | ForEach-Object { $_.Substring(0, [Math]::Min(500, $_.Length)) }"
) else (
    echo ⚠️ 统一状态文件不存在: %stateFile%
    echo 💡 这可能是正常的，如果还没有运行过监控
)

echo.
echo   3. 检查相关源代码修复...
echo 🔍 检查RefreshPositionDataAsync的线程安全修复:
findstr /n "Application.Current.Dispatcher.InvokeAsync" "Views\AutoMonitorConfigWindowSimple.xaml.cs" | findstr "RefreshPositionDataAsync" -A 5 -B 5
if %ERRORLEVEL% equ 0 (
    echo ✅ 找到线程安全的UI更新代码
) else (
    echo ⚠️ 未找到预期的线程安全代码
)

echo.
echo 🔍 检查AutoMonitorDashboard的初始化修复:
findstr /n "LoadFromMonitoringStatesFileAsync" "Views\AutoMonitorDashboard.xaml.cs" | head -3
if %ERRORLEVEL% equ 0 (
    echo ✅ 找到统一状态文件加载代码
) else (
    echo ⚠️ 未找到预期的状态文件加载代码
)

echo.
echo   4. 启动应用程序进行实际测试...
echo 💡 请按照以下步骤进行测试:
echo    a) 打开应用程序主界面
echo    b) 查看是否出现线程访问错误
echo    c) 检查是否正确加载了统一状态文件内容
echo    d) 尝试刷新数据，观察是否还有线程冲突
echo.

echo 🚀 启动应用程序...
start "" "bin\Debug\net6.0-windows\BinanceFuturesTrader.exe"

echo.
echo 📊 修复验证指南:
echo ============================================
echo.
echo 🎯 验证目标1: 线程访问修复
echo   - 观察日志中是否还有"调用线程无法访问此对象"错误
echo   - 刷新持仓数据时应该不再出现线程冲突
echo   - UI更新应该流畅，无卡顿
echo.
echo 🎯 验证目标2: 统一状态文件加载
echo   - 界面打开时应该显示"尝试从contract_monitoring_states.json加载统一状态"
echo   - 如果文件存在且有内容，应该显示"成功从统一状态文件加载配置"
echo   - 界面应该显示文件中保存的合约状态
echo.
echo 🎯 验证目标3: 错误处理增强
echo   - 即使出现异常，也应该有友好的错误提示
echo   - 应用程序不应该崩溃或卡死
echo   - 重试机制应该能够自动恢复
echo.

echo 🔧 如果仍有问题，请检查:
echo   1. 确保账户配置正确
echo   2. 检查网络连接状态
echo   3. 查看应用程序日志文件
echo   4. 确认API权限设置
echo.

pause
echo.
echo 🎉 验证完成！如果没有线程访问错误且成功加载了统一状态文件，说明修复成功。 