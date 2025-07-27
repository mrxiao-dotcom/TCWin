@echo off
chcp 65001 > nul
echo 🔧 数据源统一修复验证脚本
echo.

echo 📋 修复目标：
echo   移除所有非contract_monitoring_states.json的数据源后门路径
echo   确保UI数据完全来源于统一的状态文件
echo.

echo 🧹 步骤1: 清理所有配置文件...
if exist "%APPDATA%\BinanceFuturesTrader" (
    rmdir /s /q "%APPDATA%\BinanceFuturesTrader"
    echo ✅ 已清理整个BinanceFuturesTrader配置目录
) else (
    echo ℹ️ 配置目录不存在，跳过清理
)

echo.
echo 🔨 步骤2: 编译项目...
dotnet build BinanceFuturesTrader.csproj --configuration Debug --verbosity minimal

if %ERRORLEVEL% NEQ 0 (
    echo ❌ 编译失败！请检查代码错误
    pause
    exit /b 1
)

echo ✅ 编译成功！
echo.

echo 🚀 步骤3: 启动程序进行验证...
echo.
echo 📋 验证要点：
echo.
echo 🎯 主要验证项目：
echo   1. 没有状态文件时UI应该为空（不显示任何数据）
echo   2. 有持仓时应该自动生成状态文件，然后显示UI
echo   3. UI数据应该完全来源于状态文件
echo.
echo 🔍 具体测试步骤：
echo.
echo 【测试1：空状态验证】
echo   1. 确保没有任何配置文件存在
echo   2. 启动程序，打开"启动盯盘面板"
echo   3. 检查：合约配置区应该为空（无任何数据显示）
echo   4. 预期：不应有示例数据或其他来源的数据
echo.
echo 【测试2：状态文件生成验证】
echo   1. 在有实际持仓的情况下重新打开面板
echo   2. 检查：是否自动生成contract_monitoring_states.json文件
echo   3. 检查：UI数据是否与文件内容完全一致
echo   4. 预期：文件生成后UI立即显示对应数据
echo.
echo 【测试3：数据源唯一性验证】
echo   1. 手动删除状态文件
echo   2. 重新打开面板
echo   3. 检查：UI是否变为空白（无数据显示）
echo   4. 预期：不应从其他路径获取数据
echo.

echo 请按任意键启动程序进行测试...
pause > nul

start "" "bin\Debug\net8.0-windows\BinanceFuturesTrader.exe"

echo.
echo 🔍 程序已启动，请按照上述步骤进行验证：
echo.
echo 📁 关键文件位置：
echo   状态文件: %%APPDATA%%\BinanceFuturesTrader\Accounts\{账户名}\contract_monitoring_states.json
echo   基础配置: %%APPDATA%%\BinanceFuturesTrader\Global\auto_monitor_configs.json
echo.
echo 🚨 修复前可能存在的后门路径（现已移除）：
echo   ❌ CreateExampleContractData() - 示例数据创建
echo   ❌ CreateContractMonitorFromProfile() - 从旧PositionProfile创建
echo   ❌ _autoMonitorService.GetPositionProfiles() - 旧档案系统
echo.
echo ✅ 修复后的统一数据流程：
echo   📄 contract_monitoring_states.json → ConvertStateToContractMonitor → UI显示
echo.

echo 验证完成后请按任意键查看结果总结...
pause > nul

echo.
echo 📊 验证结果检查清单：
echo.
echo □ 测试1：没有状态文件时UI为空
echo □ 测试2：有持仓时自动生成状态文件
echo □ 测试3：删除状态文件后UI变空
echo □ 文件内容与UI显示完全一致
echo □ 触发金额正确显示（95而不是0）
echo.
echo 如发现任何问题，请检查日志输出！
echo.
echo �� 数据源统一修复验证完成！
pause 